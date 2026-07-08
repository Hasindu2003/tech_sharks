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
    public class ReviewTransfersModel : PageModel
    {
        private readonly ITransferRequestService _transferService;
        private readonly UserManager<ApplicationUser> _userManager;

        public ReviewTransfersModel(ITransferRequestService transferService, UserManager<ApplicationUser> userManager)
        {
            _transferService = transferService;
            _userManager = userManager;
        }

        public List<TransferRequestViewModel> FinalizationQueue { get; set; } = new();
        public List<TransferRequestViewModel> IncomingRequests { get; set; } = new();
        public List<TransferRequestViewModel> OutgoingRequests { get; set; } = new();
        public string HRBranch { get; set; } = string.Empty;

        public async Task<IActionResult> OnGetAsync()
        {
            var hrUser = await _userManager.GetUserAsync(User);
            if (hrUser == null) return Challenge();

            HRBranch = hrUser.Branch;

            // Requests awaiting HR finalization (all branches)
            FinalizationQueue = await _transferService.GetRequestsForHRFinalizationAsync();

            // All requests involving this HR's branch for visibility
            var all = await _transferService.GetRequestsForHRManagerAsync(hrUser.Branch);
            IncomingRequests = all.Where(r => r.RequestedBranch == hrUser.Branch).ToList();
            OutgoingRequests = all.Where(r => r.CurrentBranch == hrUser.Branch).ToList();

            return Page();
        }
    }
}
