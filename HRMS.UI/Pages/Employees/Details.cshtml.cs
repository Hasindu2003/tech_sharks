using HRMS.Domain.Entities.Core;
using HRMS.Domain.Entities.Payroll;
using HRMS.Infrastructure.Identity;
using HRMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HRMS.UI.Pages.Employees
{
    using Employee = HRMS.Domain.Entities.Core.Employee;

    [Authorize(Roles = "HR Manager,HR Officer,Area Manager,Branch Manager,Admin,Welfare Manager,Department Head")]
    public class DetailsModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public DetailsModel(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public Employee Employee { get; set; } = null!;
        public IList<EmployeeDocument> Documents { get; set; } = new List<EmployeeDocument>();
        public PayrollSalary? CurrentSalary { get; set; }
        public List<PayrollSalary> SalaryHistory { get; set; } = new();

        [BindProperty] public decimal BasicSalary { get; set; }
        [BindProperty] public string? BankName { get; set; }
        [BindProperty] public string? BankAccountName { get; set; }
        [BindProperty] public string? BankAccountNumber { get; set; }

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (!id.HasValue || id.Value <= 0)
                return NotFound();

            int empId = id.Value;
            var emp = await _context.Employees
                .Include(e => e.Department)
                .Include(e => e.Designation)
                .Include(e => e.Branch)
                .Include(e => e.ReportingOfficer)
                .FirstOrDefaultAsync(e => e.Id == empId);

            if (emp == null)
                return NotFound();

            if (User.IsInRole("HR Officer") || User.IsInRole("Area Manager"))
            {
                var currentUser = await _userManager.GetUserAsync(User);
                var allowed = ParseManagedBranches(currentUser?.ManagedBranches);
                if (allowed != null && !allowed.Contains(emp.BranchId))
                    return Forbid();
            }
            else if (User.IsInRole("Branch Manager"))
            {
                var currentUser = await _userManager.GetUserAsync(User);
                if (!string.IsNullOrWhiteSpace(currentUser?.Branch) &&
                    !string.Equals(emp.Branch?.Name, currentUser.Branch, StringComparison.OrdinalIgnoreCase))
                    return Forbid();
            }

            Employee = emp;
            Documents = await _context.EmployeeDocuments
                .Where(d => d.EmployeeId == empId)
                .OrderByDescending(d => d.UploadedAt)
                .ToListAsync();

            var salaries = await _context.PayrollSalaries
                .Where(s => s.EmployeeId == empId)
                .OrderByDescending(s => s.EffectiveDate)
                .ThenByDescending(s => s.Id)
                .ToListAsync();

            CurrentSalary = salaries.FirstOrDefault();
            SalaryHistory = salaries;

            if (CurrentSalary != null)
            {
                BasicSalary = CurrentSalary.BasicSalary;
            }

            BankName = (emp.BankName == "-" ? "" : emp.BankName);
            BankAccountName = (emp.BankAccountName == "-" ? "" : emp.BankAccountName);
            BankAccountNumber = (emp.BankAccountNumber == "-" ? "" : emp.BankAccountNumber);

            return Page();
        }

        public async Task<IActionResult> OnPostUpdateSalaryAsync(int id)
        {
            var emp = await _context.Employees.FindAsync(id);
            if (emp == null)
                return NotFound();

            // Allow HR Manager and HR Officer
            if (!User.IsInRole("HR Manager") && !User.IsInRole("HR Officer"))
            {
                return Forbid();
            }

            if (User.IsInRole("HR Officer"))
            {
                var currentUser = await _userManager.GetUserAsync(User);
                var allowed = ParseManagedBranches(currentUser?.ManagedBranches);
                if (allowed != null && !allowed.Contains(emp.BranchId))
                    return Forbid();
            }

            var newSalary = new PayrollSalary
            {
                EmployeeId = id,
                BasicSalary = BasicSalary,
                EffectiveDate = DateTime.Now
            };

            if (!string.IsNullOrWhiteSpace(BankName))
                emp.BankName = BankName.Trim();

            if (!string.IsNullOrWhiteSpace(BankAccountName))
                emp.BankAccountName = BankAccountName.Trim();

            if (!string.IsNullOrWhiteSpace(BankAccountNumber))
                emp.BankAccountNumber = BankAccountNumber.Trim();

            _context.PayrollSalaries.Add(newSalary);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Salary and banking details updated successfully!";

            return Redirect($"/Employees/Details/{id}#salary");
        }

        private static List<int>? ParseManagedBranches(string? csv)
        {
            if (string.IsNullOrWhiteSpace(csv)) return null;
            return csv.Split(',', StringSplitOptions.RemoveEmptyEntries)
                      .Select(s => int.TryParse(s.Trim(), out var id) ? id : 0)
                      .Where(id => id > 0)
                      .ToList();
        }
    }
}
