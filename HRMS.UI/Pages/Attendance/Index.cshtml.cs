using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HRMS.Infrastructure.Identity;
using HRMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HRMS.UI.Pages.Attendance
{
    [Authorize]
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public List<Domain.Entities.Attendance.Attendance> Attendances { get; set; } = new();
        public List<Domain.Entities.Core.Employee> Employees { get; set; } = new();
        public List<Domain.Entities.Core.Branch> ManagedBranchesList { get; set; } = new();
        public List<Domain.Entities.Core.Department> DepartmentsList { get; set; } = new();
        public bool IsManager { get; set; }
        public bool ShowBranchFilter { get; set; }
        public bool ShowDepartmentFilter { get; set; }
        public string BranchFilterPlaceholder { get; set; } = "-- All Branches --";
        public int CurrentUserEmployeeId { get; set; }

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

        public IndexModel(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> OnGetAsync()
        {
            if (User.IsInRole("Admin"))
            {
                return Forbid();
            }

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return Challenge();

            // Determine if the user is a manager (can filter/view others)
            IsManager = User.IsInRole("HR Manager") || 
                        User.IsInRole("HR Officer") || 
                        User.IsInRole("Area Manager") || 
                        User.IsInRole("Branch Manager") || 
                        User.IsInRole("Department Head");

            ShowBranchFilter = User.IsInRole("HR Manager") || 
                               User.IsInRole("HR Officer") || 
                               User.IsInRole("Area Manager");

            ShowDepartmentFilter = User.IsInRole("HR Manager") || 
                                   User.IsInRole("HR Officer") || 
                                   User.IsInRole("Area Manager") || 
                                   User.IsInRole("Branch Manager");

            // Look up the employee record for this user
            var employeeRecord = await _context.Employees.FirstOrDefaultAsync(e => e.Email == currentUser.Email);
            CurrentUserEmployeeId = employeeRecord?.Id ?? 0;

            // Setup base query for logs
            var query = _context.Attendances
                .Include(a => a.Employee)
                    .ThenInclude(e => e.Branch)
                .Include(a => a.Employee)
                    .ThenInclude(e => e.Department)
                .AsQueryable();

            // Set up employee dropdown query for filters
            var employeeQuery = _context.Employees
                .Where(e => e.Status != "Draft" && e.NIC != "DUTY-ACC")
                .AsQueryable();

            if (IsManager)
            {
                if (ShowDepartmentFilter)
                {
                    DepartmentsList = await _context.Departments.OrderBy(d => d.Name).ToListAsync();
                }

                if (User.IsInRole("HR Manager"))
                {
                    // HR Manager: access to all branches & departments
                    BranchFilterPlaceholder = "-- All Branches --";
                    ManagedBranchesList = await _context.Branches.OrderBy(b => b.Name).ToListAsync();

                    if (FilterBranchId.HasValue && FilterBranchId.Value > 0)
                    {
                        query = query.Where(a => a.Employee.BranchId == FilterBranchId.Value);
                        employeeQuery = employeeQuery.Where(e => e.BranchId == FilterBranchId.Value);
                    }
                }
                else if (User.IsInRole("Area Manager") || User.IsInRole("HR Officer"))
                {
                    // Area Manager & HR Officer: see designated assigned branches
                    BranchFilterPlaceholder = "-- All Assigned Branches --";
                    var managedStr = currentUser.ManagedBranches ?? "";
                    var assignedBranchIds = managedStr
                        .Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(s => int.TryParse(s.Trim(), out var id) ? id : 0)
                        .Where(id => id > 0)
                        .ToList();

                    if (!assignedBranchIds.Any()) assignedBranchIds.Add(-1); // Safety fallback

                    ManagedBranchesList = await _context.Branches
                        .Where(b => assignedBranchIds.Contains(b.Id))
                        .OrderBy(b => b.Name)
                        .ToListAsync();

                    if (FilterBranchId.HasValue && FilterBranchId.Value > 0 && assignedBranchIds.Contains(FilterBranchId.Value))
                    {
                        query = query.Where(a => a.Employee.BranchId == FilterBranchId.Value);
                        employeeQuery = employeeQuery.Where(e => e.BranchId == FilterBranchId.Value);
                    }
                    else
                    {
                        query = query.Where(a => assignedBranchIds.Contains(a.Employee.BranchId));
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

                    query = query.Where(a => a.Employee.BranchId == scopedBranchId);
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

                    query = query.Where(a => a.Employee.BranchId == scopedBranchId && a.Employee.DepartmentId == scopedDeptId);
                    employeeQuery = employeeQuery.Where(e => e.BranchId == scopedBranchId && e.DepartmentId == scopedDeptId);
                }

                // Apply selected department filter to employee list and attendance list
                if (FilterDepartmentId.HasValue && FilterDepartmentId.Value > 0)
                {
                    query = query.Where(a => a.Employee.DepartmentId == FilterDepartmentId.Value);
                    employeeQuery = employeeQuery.Where(e => e.DepartmentId == FilterDepartmentId.Value);
                }

                Employees = await employeeQuery.OrderBy(e => e.FullName).ToListAsync();
            }
            else
            {
                // Non-manager (regular employee): only see own attendance logs
                EmployeeId = CurrentUserEmployeeId;
                query = query.Where(a => a.EmployeeId == CurrentUserEmployeeId);
            }

            // Apply selected employee filter
            if (EmployeeId.HasValue && EmployeeId.Value > 0)
            {
                query = query.Where(a => a.EmployeeId == EmployeeId.Value);
            }

            // Apply date filters
            if (FromDate.HasValue)
            {
                query = query.Where(a => a.Date >= FromDate.Value);
            }
            if (ToDate.HasValue)
            {
                query = query.Where(a => a.Date <= ToDate.Value);
            }

            Attendances = await query.OrderByDescending(a => a.Date).ToListAsync();
            return Page();
        }
    }
}
