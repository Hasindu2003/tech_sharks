using HRMS.Infrastructure.Identity;
using HRMS.Application.Models;
using HRMS.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HRMS.UI.Pages.HRManager
{
    [Authorize(Roles = "HR Manager")]
    public class ReviewResignationsModel : PageModel
    {
        private readonly IResignationService _resignationService;
        private readonly UserManager<ApplicationUser> _userManager;

        public ReviewResignationsModel(IResignationService resignationService, UserManager<ApplicationUser> userManager)
        {
            _resignationService = resignationService;
            _userManager = userManager;
        }

        public List<ResignationRequestViewModel> PendingRequests { get; set; } = new();
        public List<ResignationRequestViewModel> ApprovedRequests { get; set; } = new();

        public async Task<IActionResult> OnGetAsync()
        {
            PendingRequests  = await _resignationService.GetPendingForHRManagerAsync();
            // Also show all HR-approved ones so HR can process effective dates & reactivate
            var all = await _resignationService.GetAllAsync();
            ApprovedRequests = all.Where(r =>
                r.Status == ResignationStatusEnum.HRApproved ||
                r.Status == ResignationStatusEnum.Completed).ToList();
            return Page();
        }

        public async Task<IActionResult> OnPostApproveAsync(int id, string comments)
        {
            if (string.IsNullOrWhiteSpace(comments))
            {
                TempData["ErrorMessage"] = "Comments are required for final approval.";
                return RedirectToPage();
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var success = await _resignationService.HRManagerApproveAsync(id, comments, user.Email!);
            TempData[success ? "SuccessMessage" : "ErrorMessage"] =
                success ? "Resignation officially approved. Acceptance letter generated and employee notified." : "Unable to process.";

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostRejectAsync(int id, string comments)
        {
            if (string.IsNullOrWhiteSpace(comments))
            {
                TempData["ErrorMessage"] = "A rejection reason is mandatory.";
                return RedirectToPage();
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var success = await _resignationService.HRManagerRejectAsync(id, comments, user.Email!);
            TempData[success ? "SuccessMessage" : "ErrorMessage"] =
                success ? "Resignation request has been rejected." : "Unable to process.";

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostProcessEffectiveDateAsync(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var (success, error) = await _resignationService.ProcessEffectiveDateAsync(id, user.Email!, _userManager);
            TempData[success ? "SuccessMessage" : "ErrorMessage"] =
                success ? "Employee account has been deactivated. Resignation process completed." : error;

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostReactivateAsync(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var (success, error) = await _resignationService.ReactivateAccountAsync(id, user.Email!, _userManager);
            TempData[success ? "SuccessMessage" : "ErrorMessage"] =
                success ? "Employee account has been successfully reactivated." : error;

            return RedirectToPage();
        }
    }
}
