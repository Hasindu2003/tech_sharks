using HRMS.Application.Models;
using HRMS.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HRMS.UI.Pages.HRManager
{
    [Authorize(Roles = "HR Manager")]
    public class ReviewTransferModel : PageModel
    {
        private readonly ITransferRequestService _transferService;

        public ReviewTransferModel(ITransferRequestService transferService)
        {
            _transferService = transferService;
        }

        public TransferRequestViewModel? TransferRequest { get; set; }

        public async Task<IActionResult> OnGetAsync(int id)
        {
            TransferRequest = await _transferService.GetRequestByIdAsync(id);
            if (TransferRequest == null) return NotFound();
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

            bool approved = action == "approve";
            var ok = await _transferService.HRManagerReviewAsync(id, approved, comments.Trim());

            if (!ok)
            {
                TempData["ErrorMessage"] = "Unable to process review. The request may already be at a different stage.";
                return RedirectToPage("/HRManager/ReviewTransfers");
            }

            TempData["SuccessMessage"] = approved
                ? "Transfer request finalized and fully approved."
                : "Transfer request rejected at finalization.";

            return RedirectToPage("/HRManager/ReviewTransfers");
        }
    }
}
