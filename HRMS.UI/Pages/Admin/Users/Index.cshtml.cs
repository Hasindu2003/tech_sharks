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

namespace HRMS.UI.Pages.Admin.Users
{
    [Authorize(Roles = "Admin")]
    public class IndexModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;

        public IndexModel(UserManager<ApplicationUser> userManager, ApplicationDbContext context)
        {
            _userManager = userManager;
            _context = context;
        }

        public List<UserViewModel> Users { get; set; } = new();
        public string[] AllRoles { get; set; } = Array.Empty<string>();
        
        [TempData]
        public string? SuccessMessage { get; set; }
        
        [TempData]
        public string? ErrorMessage { get; set; }

        public async Task OnGetAsync()
        {
            AllRoles = new[] { "Admin", "HR Manager", "HR Officer", "Area Manager", "Branch Manager", "Department Head" };
            
            var employees = await _context.Employees.OrderBy(e => e.FullName).ToListAsync();
            var users = await _userManager.Users.ToListAsync();
            var dutyRoleSet = new HashSet<string>(AllRoles, StringComparer.OrdinalIgnoreCase);

            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);

                // Strictly filter for duty / administrative accounts only
                bool isDutyAccount = roles.Any(r => dutyRoleSet.Contains(r))
                    || string.Equals(user.UserName, "admin", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(user.UserName, "hrmanager", StringComparison.OrdinalIgnoreCase)
                    || (user.UserName != null && (
                        user.UserName.StartsWith("bm.", StringComparison.OrdinalIgnoreCase) ||
                        user.UserName.StartsWith("am.", StringComparison.OrdinalIgnoreCase) ||
                        user.UserName.StartsWith("dh.", StringComparison.OrdinalIgnoreCase) ||
                        user.UserName.StartsWith("hro.", StringComparison.OrdinalIgnoreCase)));

                if (!isDutyAccount)
                    continue;

                var emp = user.EmployeeId != null
                    ? employees.FirstOrDefault(e => e.Id == user.EmployeeId.Value)
                    : null;

                Users.Add(new UserViewModel
                {
                    Id = user.Id,
                    UserName = user.UserName ?? "",
                    FullName = user.FullName,
                    Roles = roles.ToList(),
                    LinkedEmployee = emp != null ? emp.FullName : (!string.IsNullOrWhiteSpace(user.FullName) ? user.FullName : "-")
                });
            }

            Users = Users.OrderBy(u => u.UserName).ToList();
        }

        public async Task<IActionResult> OnPostResetPasswordAsync(string userId, string newPassword, bool requireChangeOnLogin = true)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                ErrorMessage = "User not found.";
                return RedirectToPage();
            }

            if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 6)
            {
                ErrorMessage = "Password must be at least 6 characters long.";
                return RedirectToPage();
            }

            var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
            var result = await _userManager.ResetPasswordAsync(user, resetToken, newPassword);

            if (result.Succeeded)
            {
                user.MustChangePassword = requireChangeOnLogin;
                await _userManager.UpdateAsync(user);
                SuccessMessage = $"Password for user '{user.UserName}' has been reset successfully to: {newPassword}";
            }
            else
            {
                ErrorMessage = string.Join(", ", result.Errors.Select(e => e.Description));
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDeleteUserAsync(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                ErrorMessage = "User not found.";
                return RedirectToPage();
            }

            var result = await _userManager.DeleteAsync(user);
            if (result.Succeeded)
            {
                SuccessMessage = $"User '{user.UserName}' deleted successfully.";
            }
            else
            {
                ErrorMessage = $"Failed to delete user: {string.Join(", ", result.Errors.Select(e => e.Description))}";
            }

            return RedirectToPage();
        }

        public class UserViewModel
        {
            public string Id { get; set; } = "";
            public string UserName { get; set; } = "";
            public string FullName { get; set; } = "";
            public List<string> Roles { get; set; } = new();
            public string LinkedEmployee { get; set; } = "";
        }
    }
}
