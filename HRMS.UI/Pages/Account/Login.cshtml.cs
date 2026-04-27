using HRMS.Infrastructure.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;

namespace HRMS.UI.Pages.Account
{
    public class LoginModel : PageModel
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;

        public LoginModel(SignInManager<ApplicationUser> signInManager, UserManager<ApplicationUser> userManager)
        {
            _signInManager = signInManager;
            _userManager = userManager;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public string? ErrorMessage { get; set; }

        public class InputModel
        {
            [Required(ErrorMessage = "Email address is required.")]
            [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
            [RegularExpression(@"^[a-zA-Z0-9._%+-]+@kanrich\.lk$",
                ErrorMessage = "Only @kanrich.lk email addresses are allowed.")]
            [Display(Name = "Email")]
            public string Email { get; set; } = string.Empty;

            [Required(ErrorMessage = "Password is required.")]
            [StringLength(100, MinimumLength = 8,
                ErrorMessage = "Password must be at least 8 characters long.")]
            [DataType(DataType.Password)]
            [Display(Name = "Password")]
            public string Password { get; set; } = string.Empty;

            [Display(Name = "Remember me?")]
            public bool RememberMe { get; set; }
        }

        public async Task<IActionResult> OnGetAsync()
        {
            if (User.Identity?.IsAuthenticated == true)
                return await RedirectByRoleForCurrentUser();

            await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
                return Page();

            var result = await _signInManager.PasswordSignInAsync(
                Input.Email, Input.Password, Input.RememberMe, lockoutOnFailure: true);

            if (result.Succeeded)
                return await RedirectByRoleForEmail(Input.Email);
            else if (result.IsLockedOut)
                ErrorMessage = "Your account has been locked due to multiple failed login attempts. Please try again after 5 minutes.";
            else if (result.IsNotAllowed)
                ErrorMessage = "Your account is not activated. Please contact your administrator.";
            else
                ErrorMessage = "Invalid email or password. Please check your credentials and try again.";

            return Page();
        }

        private async Task<IActionResult> RedirectByRoleForCurrentUser()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToPage("/Index");
            return await RedirectByRoles(user);
        }

        private async Task<IActionResult> RedirectByRoleForEmail(string email)
        {
            var user = await _userManager.FindByEmailAsync(email);
            if (user == null) return RedirectToPage("/Index");
            return await RedirectByRoles(user);
        }

        private async Task<IActionResult> RedirectByRoles(ApplicationUser user)
        {
            var roles = await _userManager.GetRolesAsync(user);

            if (roles.Contains("Admin"))
                return RedirectToPage("/Payroll/Index");          // ✅ fixed

            if (roles.Contains("BranchDGM"))
                return RedirectToPage("/Welfare/Approvals/BranchDGMApproval");

            if (roles.Contains("HODGM"))
                return RedirectToPage("/Welfare/Approvals/HODGMApproval");

            if (roles.Contains("SeniorManagement"))
                return RedirectToPage("/Welfare/Approvals/SeniorManagementApproval");

            if (roles.Contains("Finance"))
                return RedirectToPage("/Welfare/Approvals/FinanceApproval");

            if (roles.Contains("Employee"))
                return RedirectToPage("/Welfare/RequestList");

            return RedirectToPage("/Index");
        }
    }
}
