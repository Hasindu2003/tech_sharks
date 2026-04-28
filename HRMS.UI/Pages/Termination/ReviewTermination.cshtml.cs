using HRMS.Infrastructure.Identity;
using HRMS.UI.Models;
using HRMS.UI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HRMS.UI.Pages.Termination
{
    [Authorize(Roles = "HR Manager,Area Manager,Branch Manager")]
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

        public async Task<IActionResult> OnGetAsync(int id)
        {
            Request = await _terminationService.GetTerminationByIdAsync(id);
            if (Request == null) return NotFound();
            if (Request.Status != TerminationStatusEnum.SubmittedForApproval)
                return RedirectToPage("/Termination/Details", new { id });
            return Page();
        }

        public async Task<IActionResult> OnPostApproveAsync(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            await _terminationService.ApproveTerminationAsync(id, Comments, user.Email!);

            TempData["SuccessMessage"] = "Termination request has been approved and sent for financial clearance.";
            return RedirectToPage("/Termination/ApprovalQueue");
        }

        public async Task<IActionResult> OnPostRejectAsync(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            if (string.IsNullOrWhiteSpace(Comments))
            {
                Request = await _terminationService.GetTerminationByIdAsync(id);
                ModelState.AddModelError("Comments", "Please provide a reason for rejection.");
                return Page();
            }

            await _terminationService.RejectTerminationAsync(id, Comments, user.Email!);

            TempData["SuccessMessage"] = "Termination request has been rejected.";
            return RedirectToPage("/Termination/ApprovalQueue");
        }
    }
}
