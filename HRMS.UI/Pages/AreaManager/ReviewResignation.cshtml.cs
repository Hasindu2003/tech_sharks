using HRMS.Infrastructure.Identity;
using HRMS.Application.Models;
using HRMS.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HRMS.UI.Pages.AreaManager
{
    [Authorize(Roles = "Area Manager")]
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

        public async Task<IActionResult> OnPostAsync(int id, string action, string comments)
        {
            if (string.IsNullOrWhiteSpace(comments) || comments.Trim().Length < 5)
            {
                TempData["ErrorMessage"] = "Comments are required (minimum 5 characters).";
                return RedirectToPage(new { id });
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            bool success;
            if (action == "approve")
                success = await _resignationService.AreaManagerApproveAsync(id, comments, user.Email!);
            else
                success = await _resignationService.AreaManagerRejectAsync(id, comments, user.Email!);

            TempData[success ? "SuccessMessage" : "ErrorMessage"] = success
                ? (action == "approve" ? "Resignation approved and forwarded to HR Manager." : "Resignation request has been rejected.")
                : "Unable to process this request.";

            return RedirectToPage("/AreaManager/ReviewResignations");
        }
    }
}
