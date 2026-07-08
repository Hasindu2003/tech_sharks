using HRMS.Infrastructure.Identity;
using HRMS.Application.Models;
using HRMS.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HRMS.UI.Pages.DepartmentHead
{
    [Authorize(Roles = "Department Head")]
    public class ReviewTransferModel : PageModel
    {
        private readonly ITransferRequestService _transferService;
        private readonly UserManager<ApplicationUser> _userManager;

        public ReviewTransferModel(ITransferRequestService transferService, UserManager<ApplicationUser> userManager)
        {
            _transferService = transferService;
            _userManager = userManager;
        }

        public TransferRequestViewModel? TransferRequest { get; set; }

        public async Task<IActionResult> OnGetAsync(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            TransferRequest = await _transferService.GetRequestByIdAsync(id);

            if (TransferRequest == null) return NotFound();

            // Ensure this Dept Head is responsible for this request's branch + department
            if (TransferRequest.CurrentBranch != user?.Branch || TransferRequest.Department != user?.Department)
                return NotFound();

            return Page();
        }

        public async Task<IActionResult> OnPostAsync(int id, string action, string comments)
        {
            if (string.IsNullOrWhiteSpace(comments) || comments.Trim().Length < 10)
            {
                ModelState.AddModelError(string.Empty, "Comments must be at least 10 characters.");
                TransferRequest = await _transferService.GetRequestByIdAsync(id);
                return Page();
            }

            var user = await _userManager.GetUserAsync(User);
            var request = await _transferService.GetRequestByIdAsync(id);
            if (request == null || request.CurrentBranch != user?.Branch || request.Department != user?.Department)
                return NotFound();

            bool approved = action == "approve";
            var ok = await _transferService.DeptHeadReviewAsync(id, approved, comments.Trim());
            if (!ok)
            {
                TempData["ErrorMessage"] = "Unable to process review. The request may have already been reviewed.";
                return RedirectToPage("/DepartmentHead/ReviewTransfers");
            }

            TempData["SuccessMessage"] = approved
                ? "Transfer request approved and forwarded to Branch Managers."
                : "Transfer request rejected.";

            return RedirectToPage("/DepartmentHead/ReviewTransfers");
        }
    }
}
