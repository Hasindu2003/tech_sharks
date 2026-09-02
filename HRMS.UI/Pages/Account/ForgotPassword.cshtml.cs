using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using HRMS.Infrastructure.Services;
using HRMS.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HRMS.UI.Pages.Account
{
    public class ForgotPasswordModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IEmailService _emailService;

        public ForgotPasswordModel(UserManager<ApplicationUser> userManager, IEmailService emailService)
        {
            _userManager = userManager;
            _emailService = emailService;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public bool IsSuccess { get; set; }
        public string? DutyAccountWarning { get; set; }

        public class InputModel
        {
            [Required(ErrorMessage = "Please enter your registered email address or username.")]
            [Display(Name = "Email or Username")]
            public string Identifier { get; set; } = string.Empty;
        }

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            var identifier = Input.Identifier.Trim();
            var user = await _userManager.FindByEmailAsync(identifier) ?? await _userManager.FindByNameAsync(identifier);

            if (user == null)
            {
                // To prevent user enumeration, show generic success message
                IsSuccess = true;
                return Page();
            }

            var roles = await _userManager.GetRolesAsync(user);
            var isDutyAccount = roles.Any(r => r is "Admin" or "HR Manager" or "Area Manager" or "Branch Manager" or "Department Head" or "HR Officer");

            if (isDutyAccount)
            {
                DutyAccountWarning = "Duty account and administrative credentials cannot be self-reset via email. Please contact the System Administrator to reset your duty account password.";
                return Page();
            }

            // Employee self-service reset
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var resetUrl = Url.Page(
                "/Account/ResetPassword",
                pageHandler: null,
                values: new { userId = user.Id, code = token },
                protocol: Request.Scheme);

            // Send password reset email
            await _emailService.SendPasswordResetLinkAsync(user.Email ?? identifier, user.FullName, resetUrl ?? "");

            IsSuccess = true;
            return Page();
        }
    }
}
