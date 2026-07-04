using HRMS.Domain.Entities.Core;
using HRMS.Infrastructure.Identity;
using HRMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HRMS.UI.Pages.Employees
{
    [Authorize]
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public IndexModel(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public IList<Employee> EmployeeList { get; set; } = default!;
        public HashSet<int> EmployeeIdsWithLogin { get; set; } = new();
        public bool CanManageLogins => User.IsInRole("Admin") || User.IsInRole("HR Manager");

        public async Task OnGetAsync()
        {
            EmployeeList = await _context.Employees
                .Include(e => e.Department)
                .Include(e => e.Designation)
                .ToListAsync();

            var linkedEmployeeIds = await _userManager.Users
                .Where(u => u.EmployeeId != null)
                .Select(u => u.EmployeeId!.Value)
                .ToListAsync();
            EmployeeIdsWithLogin = linkedEmployeeIds.ToHashSet();
        }

        public async Task<IActionResult> OnPostCreateLoginAsync(int employeeId)
        {
            if (!CanManageLogins)
                return Forbid();

            var employee = await _context.Employees.FindAsync(employeeId);
            if (employee == null)
            {
                TempData["ErrorMessage"] = "Employee not found.";
                return RedirectToPage();
            }

            var alreadyLinked = await _userManager.Users.AnyAsync(u => u.EmployeeId == employeeId);
            if (alreadyLinked)
            {
                TempData["ErrorMessage"] = "This employee already has a login.";
                return RedirectToPage();
            }

            var existingByEmail = await _userManager.FindByEmailAsync(employee.Email);
            if (existingByEmail != null)
            {
                TempData["ErrorMessage"] = $"An account already exists for {employee.Email}.";
                return RedirectToPage();
            }

            var tempPassword = GenerateTempPassword();
            var user = new ApplicationUser
            {
                UserName = employee.Email,
                Email = employee.Email,
                EmailConfirmed = true,
                EmployeeId = employee.Id,
                MustChangePassword = true
            };

            var result = await _userManager.CreateAsync(user, tempPassword);
            if (!result.Succeeded)
            {
                TempData["ErrorMessage"] = string.Join(" ", result.Errors.Select(e => e.Description));
                return RedirectToPage();
            }

            await _userManager.AddToRoleAsync(user, "Employee");

            TempData["SuccessMessage"] =
                $"Login created for {employee.Email}. Temporary password: {tempPassword} — share this with the employee now; it won't be shown again.";
            return RedirectToPage();
        }

        private static string GenerateTempPassword()
        {
            var raw = Guid.NewGuid().ToString("N")[..10];
            // Guarantees the configured Identity password policy (digit, lower, upper, non-alphanumeric, length >= 8).
            return $"{raw}Aa1!";
        }
    }
}
