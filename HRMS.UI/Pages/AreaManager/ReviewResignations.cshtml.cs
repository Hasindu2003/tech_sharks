using HRMS.Infrastructure.Identity;
using HRMS.Application.Models;
using HRMS.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HRMS.UI.Pages.AreaManager
{
    [Authorize(Roles = "Admin,Area Manager")]
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

        public async Task<IActionResult> OnGetAsync()
        {
            PendingRequests = await _resignationService.GetPendingForAreaManagerAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostApproveAsync(int id, string comments)
        {
            if (string.IsNullOrWhiteSpace(comments))
            {
                TempData["ErrorMessage"] = "Comments are required when reviewing a resignation.";
                return RedirectToPage();
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var success = await _resignationService.AreaManagerApproveAsync(id, comments, user.Email!);
            TempData[success ? "SuccessMessage" : "ErrorMessage"] =
                success ? "Resignation approved and forwarded to HR Manager." : "Unable to process this request.";

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

            var success = await _resignationService.AreaManagerRejectAsync(id, comments, user.Email!);
            TempData[success ? "SuccessMessage" : "ErrorMessage"] =
                success ? "Resignation request has been rejected." : "Unable to process this request.";

            return RedirectToPage();
        }
    }
}
