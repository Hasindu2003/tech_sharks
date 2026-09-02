using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HRMS.Domain.Entities.Attendance;
using HRMS.Domain.Entities.Core;
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
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public IndexModel(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public List<BiometricLog> BiometricLogs { get; set; } = new();
        public List<Domain.Entities.Core.Employee> Employees { get; set; } = new();
        public List<Branch> ManagedBranchesList { get; set; } = new();
        public List<Department> DepartmentsList { get; set; } = new();

        public bool ShowBranchFilter { get; set; }
        public bool ShowDepartmentFilter { get; set; }
        public string BranchFilterPlaceholder { get; set; } = "-- All Branches --";

        [BindProperty(SupportsGet = true)]
        public int? EmployeeId { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? FilterBranchId { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? FilterDepartmentId { get; set; }

        [BindProperty(SupportsGet = true)]
        public DateTime? FromDate { get; set; }

        [BindProperty(SupportsGet = true)]
        public DateTime? ToDate { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            if (User.IsInRole("Admin"))
            {
                return Forbid();
            }

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return Challenge();

            ShowBranchFilter = User.IsInRole("HR Manager") || 
                               User.IsInRole("HR Officer") || 
                               User.IsInRole("Area Manager");

            ShowDepartmentFilter = User.IsInRole("HR Manager") || 
                                   User.IsInRole("HR Officer") || 
                                   User.IsInRole("Area Manager") || 
                                   User.IsInRole("Branch Manager");

            if (ShowDepartmentFilter)
            {
                DepartmentsList = await _context.Departments.OrderBy(d => d.Name).ToListAsync();
            }

            var query = _context.BiometricLogs
                .Include(b => b.Employee)
                    .ThenInclude(e => e.Branch)
                .Include(b => b.Employee)
                    .ThenInclude(e => e.Department)
                .AsQueryable();

            var employeeQuery = _context.Employees
                .Where(e => e.Status != "Draft" && !e.NIC.StartsWith("DUTY") && e.NIC != "DUTY-ACC")
                .AsQueryable();

            if (User.IsInRole("HR Manager"))
            {
                // HR Manager: access to all branches
                BranchFilterPlaceholder = "-- All Branches --";
                ManagedBranchesList = await _context.Branches.OrderBy(b => b.Name).ToListAsync();

                if (FilterBranchId.HasValue && FilterBranchId.Value > 0)
                {
                    query = query.Where(b => b.Employee.BranchId == FilterBranchId.Value);
                    employeeQuery = employeeQuery.Where(e => e.BranchId == FilterBranchId.Value);
                }
            }
            else if (User.IsInRole("Area Manager") || User.IsInRole("HR Officer"))
            {
                // Area Manager & HR Officer: access to their assigned branches
                BranchFilterPlaceholder = "-- All Assigned Branches --";
                var managedStr = currentUser.ManagedBranches ?? "";
                var assignedBranchIds = managedStr
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => int.TryParse(s.Trim(), out var id) ? id : 0)
                    .Where(id => id > 0)
                    .ToList();

                if (!assignedBranchIds.Any()) assignedBranchIds.Add(-1);

                ManagedBranchesList = await _context.Branches
                    .Where(b => assignedBranchIds.Contains(b.Id))
                    .OrderBy(b => b.Name)
                    .ToListAsync();

                if (FilterBranchId.HasValue && FilterBranchId.Value > 0 && assignedBranchIds.Contains(FilterBranchId.Value))
                {
                    query = query.Where(b => b.Employee.BranchId == FilterBranchId.Value);
                    employeeQuery = employeeQuery.Where(e => e.BranchId == FilterBranchId.Value);
                }
                else
                {
                    query = query.Where(b => assignedBranchIds.Contains(b.Employee.BranchId));
                    employeeQuery = employeeQuery.Where(e => assignedBranchIds.Contains(e.BranchId));
                }
            }
            else if (User.IsInRole("Branch Manager"))
            {
                // Branch Manager: scoped to their own branch
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
                // Department Head: scoped to their branch and department
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

            // Apply selected department filter
            if (FilterDepartmentId.HasValue && FilterDepartmentId.Value > 0)
            {
                query = query.Where(b => b.Employee.DepartmentId == FilterDepartmentId.Value);
                employeeQuery = employeeQuery.Where(e => e.DepartmentId == FilterDepartmentId.Value);
            }

            Employees = await employeeQuery.OrderBy(e => e.FullName).ToListAsync();

            // Apply selected employee filter
            if (EmployeeId.HasValue && EmployeeId.Value > 0)
            {
                query = query.Where(b => b.EmployeeId == EmployeeId.Value);
            }

            // Apply date filters
            if (FromDate.HasValue)
            {
                query = query.Where(b => b.LogDateTime.Date >= FromDate.Value.Date);
            }
            if (ToDate.HasValue)
            {
                query = query.Where(b => b.LogDateTime.Date <= ToDate.Value.Date);
            }

            // Order by latest logs first
            BiometricLogs = await query
                .OrderByDescending(b => b.LogDateTime)
                .ThenByDescending(b => b.Id)
                .ToListAsync();

            return Page();
        }
    }
}
