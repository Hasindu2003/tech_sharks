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
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ApplicationDbContext _context;

        public IndexModel(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager, ApplicationDbContext context)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _context = context;
        }

        public List<UserViewModel> Users { get; set; } = new();
        public List<EmployeeOption> Employees { get; set; } = new();
        public string[] AllRoles { get; set; } = Array.Empty<string>();
        
        [TempData]
        public string? SuccessMessage { get; set; }
        
        [TempData]
        public string? ErrorMessage { get; set; }

        public async Task OnGetAsync()
        {
            AllRoles = new[] { "Admin", "HR Manager", "Area Manager", "Branch Manager", "Department Head", "Employee" };
            
            var employees = await _context.Employees.OrderBy(e => e.FullName).ToListAsync();
            Employees = employees.Select(e => new EmployeeOption
            {
                Id = e.Id,
                Label = $"{e.FullName} ({e.Email})"
            }).ToList();

            var users = await _userManager.Users.ToListAsync();
            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                var emp = user.EmployeeId != null
                    ? employees.FirstOrDefault(e => e.Id == user.EmployeeId.Value)
                    : null;
                Users.Add(new UserViewModel
                {
                    Id = user.Id,
                    Email = user.Email ?? "",
                    UserName = user.UserName ?? "",
                    EmailConfirmed = user.EmailConfirmed,
                    Roles = roles.ToList(),
                    LinkedEmployee = emp != null ? emp.FullName : "Not linked"
                });
            }
        }

        public async Task<IActionResult> OnPostCreateUserAsync(string email, string role, int? employeeId)
        {
            if (await _userManager.FindByEmailAsync(email) != null)
            {
                ErrorMessage = $"A user with email '{email}' already exists.";
                return RedirectToPage();
            }

            var defaultPassword = "Password@123";

            var newUser = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                EmployeeId = employeeId
            };

            var result = await _userManager.CreateAsync(newUser, defaultPassword);
            if (result.Succeeded)
            {
                await _userManager.AddToRoleAsync(newUser, role);
                SuccessMessage = $"User '{email}' created with role '{role}' successfully. Default password: {defaultPassword}";
            }
            else
            {
                ErrorMessage = string.Join(", ", result.Errors.Select(e => e.Description));
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostAssignRoleAsync(string userId, string role)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                ErrorMessage = "User not found.";
                return RedirectToPage();
            }

            var currentRoles = await _userManager.GetRolesAsync(user);
            if (currentRoles.Contains(role))
            {
                ErrorMessage = $"User already has role '{role}'.";
                return RedirectToPage();
            }

            var result = await _userManager.AddToRoleAsync(user, role);
            if (result.Succeeded)
            {
                SuccessMessage = $"Role '{role}' assigned to user.";
            }
            else
            {
                ErrorMessage = string.Join(", ", result.Errors.Select(e => e.Description));
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostRemoveRoleAsync(string userId, string role)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                ErrorMessage = "User not found.";
                return RedirectToPage();
            }

            var result = await _userManager.RemoveFromRoleAsync(user, role);
            if (result.Succeeded)
            {
                SuccessMessage = $"Role '{role}' removed from user.";
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
                SuccessMessage = "User deleted successfully.";
            }
            else
            {
                ErrorMessage = string.Join(", ", result.Errors.Select(e => e.Description));
            }

            return RedirectToPage();
        }

        public class UserViewModel
        {
            public string Id { get; set; } = "";
            public string Email { get; set; } = "";
            public string UserName { get; set; } = "";
            public bool EmailConfirmed { get; set; }
            public List<string> Roles { get; set; } = new();
            public string LinkedEmployee { get; set; } = "";
        }

        public class EmployeeOption
        {
            public int Id { get; set; }
            public string Label { get; set; } = "";
        }
    }
}
