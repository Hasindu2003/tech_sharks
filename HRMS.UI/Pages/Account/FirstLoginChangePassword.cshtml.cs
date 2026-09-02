using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using HRMS.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HRMS.UI.Pages.Account
{
    public class FirstLoginChangePasswordModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;

        public FirstLoginChangePasswordModel(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager)
        {
            _userManager = userManager;
            _signInManager = signInManager;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public string UserDisplayName { get; set; } = string.Empty;
        public string UserRoleName { get; set; } = string.Empty;

        public class InputModel
        {
            [Required(ErrorMessage = "Current temporary password is required")]
            [DataType(DataType.Password)]
            [Display(Name = "Current Password")]
            public string CurrentPassword { get; set; } = string.Empty;

            [Required(ErrorMessage = "New password is required")]
            [StringLength(100, ErrorMessage = "The {0} must be at least {2} characters long.", MinimumLength = 8)]
            [DataType(DataType.Password)]
            [Display(Name = "New Password")]
            public string NewPassword { get; set; } = string.Empty;

            [DataType(DataType.Password)]
            [Display(Name = "Confirm New Password")]
            [Compare("NewPassword", ErrorMessage = "The new password and confirmation password do not match.")]
            public string ConfirmPassword { get; set; } = string.Empty;
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToPage("/Account/Login");
            }

            if (!user.MustChangePassword)
            {
                return RedirectToDashboard(user);
            }

            UserDisplayName = user.FullName;
            var roles = await _userManager.GetRolesAsync(user);
            UserRoleName = roles.FirstOrDefault() ?? "User";

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToPage("/Account/Login");
            }

            UserDisplayName = user.FullName;
            var roles = await _userManager.GetRolesAsync(user);
            UserRoleName = roles.FirstOrDefault() ?? "User";

            if (!ModelState.IsValid)
            {
                return Page();
            }

            if (Input.CurrentPassword == Input.NewPassword)
            {
                ModelState.AddModelError("Input.NewPassword", "Your new password must be different from your temporary password.");
                return Page();
            }

            var changeResult = await _userManager.ChangePasswordAsync(user, Input.CurrentPassword, Input.NewPassword);
            if (!changeResult.Succeeded)
            {
                foreach (var error in changeResult.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
                return Page();
            }

            // Mark initial password change as completed
            user.MustChangePassword = false;
            await _userManager.UpdateAsync(user);

            // Refresh user cookie session with updated security stamp
            await _signInManager.RefreshSignInAsync(user);

            TempData["SuccessMessage"] = "Your password has been changed successfully! Welcome to Kanrich HRMS.";
            return RedirectToDashboard(user);
        }

        private IActionResult RedirectToDashboard(ApplicationUser user)
        {
            return RedirectToPage("/Index");
        }
    }
}
