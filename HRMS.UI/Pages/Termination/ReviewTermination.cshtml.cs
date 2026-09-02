using System;
using System.Threading.Tasks;
using HRMS.Application.Models;
using HRMS.Application.Services;
using HRMS.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HRMS.UI.Pages.Termination
{
    [Authorize(Roles = "HR Manager,HR Officer")]
    public class ReviewTerminationModel : PageModel
    {
        private readonly ITerminationService _terminationService;
        private readonly UserManager<ApplicationUser> _userManager;

        public ReviewTerminationModel(ITerminationService terminationService, UserManager<ApplicationUser> userManager)
        {
            _terminationService = terminationService;
            _userManager = userManager;
        }

        public new TerminationRequestViewModel? Request { get; set; }

        [BindProperty]
        public string Comments { get; set; } = string.Empty;

        public bool CanFinalize { get; set; }

        public async Task<IActionResult> OnGetAsync(int id)
        {
            Request = await _terminationService.GetTerminationByIdAsync(id);
            if (Request == null) return NotFound();

            CanFinalize = Request.Status == TerminationStatusEnum.AMApproved ||
                          Request.Status == TerminationStatusEnum.FinanceClearance ||
                          Request.Status == TerminationStatusEnum.SubmittedForApproval;

            return Page();
        }

        public async Task<IActionResult> OnPostApproveAsync(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var result = await _terminationService.FinalizeTerminationAsync(
                id,
                true,
                !string.IsNullOrWhiteSpace(Comments) ? Comments : "Finalized and approved by HR.",
                user.Email ?? user.UserName ?? "HR Officer"
            );

            if (result.Success)
            {
                TempData["SuccessMessage"] = "Termination request has been finalized. Employee status updated and notifications sent.";
                return RedirectToPage("/Termination/Requests");
            }

            TempData["ErrorMessage"] = result.ErrorMessage ?? "Failed to finalize termination.";
            return RedirectToPage(new { id });
        }

        public async Task<IActionResult> OnPostRejectAsync(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            if (string.IsNullOrWhiteSpace(Comments) || Comments.Trim().Length < 5)
            {
                Request = await _terminationService.GetTerminationByIdAsync(id);
                ModelState.AddModelError("Comments", "Please provide a reason for rejection (minimum 5 characters).");
                return Page();
            }

            var result = await _terminationService.FinalizeTerminationAsync(
                id,
                false,
                Comments.Trim(),
                user.Email ?? user.UserName ?? "HR Officer"
            );

            if (result.Success)
            {
                TempData["SuccessMessage"] = "Termination request has been rejected.";
                return RedirectToPage("/Termination/Requests");
            }

            TempData["ErrorMessage"] = result.ErrorMessage ?? "Failed to reject termination.";
            return RedirectToPage(new { id });
        }
    }
}
