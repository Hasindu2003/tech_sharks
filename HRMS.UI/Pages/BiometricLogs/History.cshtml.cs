using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HRMS.Domain.Entities.Attendance;
using HRMS.Domain.Entities.Core;
using HRMS.Domain.Common;
using HRMS.Infrastructure.Identity;
using HRMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HRMS.UI.Pages.BiometricLogs
{
    [Authorize(Roles = "Department Head, Branch Manager, Area Manager, HR Officer, HR Manager")]
    public class HistoryModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public HistoryModel(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public List<BiometricLog> BiometricLogs { get; set; } = new();
        public List<Domain.Entities.Core.Employee> Employees { get; set; } = new();
        
        [BindProperty(SupportsGet = true)]
        public int? EmployeeId { get; set; }
        
        [BindProperty(SupportsGet = true)]
        public string DateFilter { get; set; } = "All";

        public async Task<IActionResult> OnGetAsync()
        {
            if (User.IsInRole("Admin"))
            {
                return Forbid();
            }

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return Challenge();

            var query = _context.BiometricLogs
                .Include(b => b.Employee)
                    .ThenInclude(e => e.Branch)
                .Include(b => b.Employee)
                    .ThenInclude(e => e.Department)
                .AsQueryable();

            var employeeQuery = _context.Employees
                .Where(e => e.Status != "Draft" && e.NIC != "DUTY-ACC")
                .AsQueryable();

            if (User.IsInRole("HR Manager"))
            {
                // HR Manager: all branches
            }
            else if (User.IsInRole("Area Manager") || User.IsInRole("HR Officer"))
            {
                var managedStr = currentUser.ManagedBranches ?? "";
                var assignedBranchIds = managedStr
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => int.TryParse(s.Trim(), out var id) ? id : 0)
                    .Where(id => id > 0)
                    .ToList();

                if (!assignedBranchIds.Any()) assignedBranchIds.Add(-1);

                query = query.Where(b => assignedBranchIds.Contains(b.Employee.BranchId));
                employeeQuery = employeeQuery.Where(e => assignedBranchIds.Contains(e.BranchId));
            }
            else if (User.IsInRole("Branch Manager"))
            {
                int scopedBranchId = -1;
                if (!string.IsNullOrWhiteSpace(currentUser.Branch))
                {
                    var branch = await _context.Branches.FirstOrDefaultAsync(b => b.Name == currentUser.Branch);
                    scopedBranchId = branch?.Id ?? -1;
                }

                query = query.Where(b => b.Employee.BranchId == scopedBranchId);
                employeeQuery = employeeQuery.Where(e => e.BranchId == scopedBranchId);
            }
            else if (User.IsInRole("Department Head"))
            {
                int scopedBranchId = -1;
                int scopedDeptId = -1;
                if (!string.IsNullOrWhiteSpace(currentUser.Branch))
                {
                    var branch = await _context.Branches.FirstOrDefaultAsync(b => b.Name == currentUser.Branch);
                    scopedBranchId = branch?.Id ?? -1;
                }
                if (!string.IsNullOrWhiteSpace(currentUser.Department))
                {
                    var dept = await _context.Departments.FirstOrDefaultAsync(d => d.Name == currentUser.Department);
                    scopedDeptId = dept?.Id ?? -1;
                }

                query = query.Where(b => b.Employee.BranchId == scopedBranchId && b.Employee.DepartmentId == scopedDeptId);
                employeeQuery = employeeQuery.Where(e => e.BranchId == scopedBranchId && e.DepartmentId == scopedDeptId);
            }

            Employees = await employeeQuery.OrderBy(e => e.FullName).ToListAsync();
            
            if (EmployeeId.HasValue && EmployeeId.Value > 0)
            {
                query = query.Where(b => b.EmployeeId == EmployeeId.Value);
            }
            
            if (!string.IsNullOrEmpty(DateFilter) && DateFilter != "All")
            {
                var now = SriLankaTime.Now;
                if (DateFilter == "Last7Days")
                {
                    var limit = now.Date.AddDays(-7);
                    query = query.Where(b => b.LogDateTime >= limit);
                }
                else if (DateFilter == "LastMonth")
                {
                    var limit = now.Date.AddMonths(-1);
                    query = query.Where(b => b.LogDateTime >= limit);
                }
                else if (DateFilter == "Last3Months")
                {
                    var limit = now.Date.AddMonths(-3);
                    query = query.Where(b => b.LogDateTime >= limit);
                }
            }
            
            BiometricLogs = await query
                .OrderByDescending(b => b.LogDateTime)
                .ThenByDescending(b => b.Id)
                .ToListAsync();

            return Page();
        }
    }
}
