using HRMS.Infrastructure.Identity;
using HRMS.Application.Models;
using HRMS.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HRMS.UI.Pages.HRManager
{
    [Authorize(Roles = "HR Manager,HR Officer")]
    public class ReviewResignationModel : PageModel
    {
        private readonly IResignationService _resignationService;
        private readonly UserManager<ApplicationUser> _userManager;

        public ReviewResignationModel(IResignationService resignationService, UserManager<ApplicationUser> userManager)
        {
            _resignationService = resignationService;
            _userManager = userManager;
        }

        public ResignationRequestViewModel? ResignationRequest { get; set; }

        public async Task<IActionResult> OnGetAsync(int id)
        {
            ResignationRequest = await _resignationService.GetByIdAsync(id);
            if (ResignationRequest == null) return NotFound();
            return Page();
        }

        public async Task<IActionResult> OnPostAsync(int id, string action, string? comments = null)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            bool success;
            if (action == "mark_reviewed")
            {
                var remark = string.IsNullOrWhiteSpace(comments) ? "Seen and acknowledged by HR Manager" : comments.Trim();
                success = await _resignationService.HRManagerMarkAsReviewedAsync(id, remark, user.Email!);
                TempData[success ? "SuccessMessage" : "ErrorMessage"] = success
                    ? "Managerial resignation notice marked as reviewed successfully."
                    : "Unable to process review.";
                return RedirectToPage("/Separation/Dashboard", new { ActiveTab = "Resignations" });
            }

            if (string.IsNullOrWhiteSpace(comments) || comments.Trim().Length < 5)
            {
                TempData["ErrorMessage"] = "Comments are required (minimum 5 characters).";
                return RedirectToPage(new { id });
            }

            if (action == "approve")
                success = await _resignationService.HRManagerApproveAsync(id, comments.Trim(), user.Email!);
            else
                success = await _resignationService.HRManagerRejectAsync(id, comments.Trim(), user.Email!);

            TempData[success ? "SuccessMessage" : "ErrorMessage"] = success
                ? (action == "approve" ? "Resignation officially approved and employee notified." : "Resignation request has been rejected.")
                : "Unable to process.";

            return RedirectToPage("/HRManager/ReviewResignations");
        }

        public async Task<IActionResult> OnPostProcessEffectiveDateAsync(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var (success, error) = await _resignationService.ProcessEffectiveDateAsync(id, user.Email!, _userManager);
            TempData[success ? "SuccessMessage" : "ErrorMessage"] =
                success ? "Employee account deactivated. Resignation process completed." : error;

            return RedirectToPage(new { id });
        }

        public async Task<IActionResult> OnPostReactivateAsync(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var (success, error) = await _resignationService.ReactivateAccountAsync(id, user.Email!, _userManager);
            TempData[success ? "SuccessMessage" : "ErrorMessage"] =
                success ? "Employee account reactivated successfully." : error;

            return RedirectToPage(new { id });
        }
    }
}
