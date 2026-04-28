using HRMS.Infrastructure.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;
using HRMS.Infrastructure.Identity;

namespace HRMS.UI.Pages.Account
{
    public class LoginModel : PageModel
    {
        private readonly SignInManager<ApplicationUser> _signInManager;

        public LoginModel(SignInManager<ApplicationUser> signInManager)
        {
            _signInManager = signInManager;
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
        public string? ReturnUrl { get; set; }

        public class InputModel
        {
            [Required]
            [EmailAddress]
            public string Email { get; set; } = string.Empty;

            [Required]
            [DataType(DataType.Password)]
            public string Password { get; set; } = string.Empty;

            [Display(Name = "Remember me?")]
            public bool RememberMe { get; set; }
        }

        public async Task OnGetAsync()
        {
            // Clear the existing external cookie
            await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
        }

        public async Task<IActionResult> OnPostAsync()
        {
        public async Task OnGetAsync(string? returnUrl = null)
        {
            ReturnUrl = returnUrl;
            await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
        }

        public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");

            if (!ModelState.IsValid)
                return Page();

            var result = await _signInManager.PasswordSignInAsync(
                Input.Email, Input.Password, Input.RememberMe, lockoutOnFailure: true);

            if (result.Succeeded)
            {
                return RedirectToPage("/Index");
            }
            else if (result.IsLockedOut)
            {
                ErrorMessage = "Your account has been locked due to multiple failed login attempts. Please try again after 5 minutes.";
                return Page();
            }
            else if (result.IsNotAllowed)
            {
                ErrorMessage = "Your account is not activated. Please contact your administrator.";
                return Page();
            }
            else
            {
                ErrorMessage = "Invalid email or password. Please check your credentials and try again.";
                return Page();
            }
                Input.Email, Input.Password, Input.RememberMe, lockoutOnFailure: false);

            if (result.Succeeded)
                return LocalRedirect(returnUrl);

            ModelState.AddModelError(string.Empty, "Invalid email or password.");
            return Page();
        }
    }
}
