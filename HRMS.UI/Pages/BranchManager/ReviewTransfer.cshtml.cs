using HRMS.Infrastructure.Identity;
using HRMS.Application.Models;
using HRMS.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HRMS.UI.Pages.BranchManager
{
    [Authorize(Roles = "Branch Manager")]
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
        public string ReviewerRole { get; set; } = ""; // "Current Branch Manager" or "Target Branch Manager"

        public async Task<IActionResult> OnGetAsync(int id)
        {
            TransferRequest = await _transferService.GetRequestByIdAsync(id);
            if (TransferRequest == null) return NotFound();

            var user = await _userManager.GetUserAsync(User);
            var branch = user?.Branch ?? "";
            if (TransferRequest.CurrentBranch == branch)
                ReviewerRole = "Current Branch Manager";
            else if (TransferRequest.RequestedBranch == branch)
                ReviewerRole = "Target Branch Manager";
            else
                return NotFound(); // Not authorized for this request

            return Page();
        }

        public async Task<IActionResult> OnPostAsync(int id, string action, string comments)
        {
            if (string.IsNullOrWhiteSpace(comments) || comments.Trim().Length < 10)
            {
                ModelState.AddModelError(string.Empty, "Comments must be at least 10 characters.");
                TransferRequest = await _transferService.GetRequestByIdAsync(id);
                var user2 = await _userManager.GetUserAsync(User);
                ReviewerRole = TransferRequest?.CurrentBranch == user2?.Branch ? "Current Branch Manager" : "Target Branch Manager";
                return Page();
            }

            var user = await _userManager.GetUserAsync(User);
            var branch = user?.Branch ?? "";
            bool approved = action == "approve";
            var ok = await _transferService.BranchManagerReviewAsync(id, approved, comments.Trim(), branch);

            if (!ok)
            {
                TempData["ErrorMessage"] = "Unable to process review. The request may already be at a different stage.";
                return RedirectToPage("/BranchManager/ReviewTransfers");
            }

            TempData["SuccessMessage"] = approved
                ? "Transfer request approved."
                : "Transfer request rejected.";

            return RedirectToPage("/BranchManager/ReviewTransfers");
        }
    }
}