using HRMS.Infrastructure.Identity;
using HRMS.UI.Models;
using HRMS.UI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HRMS.UI.Pages.BranchManager
{
    [Authorize(Roles = "Branch Manager")]
    public class ReviewTransfersModel : PageModel
    {
        private readonly ITransferRequestService _transferService;
        private readonly UserManager<ApplicationUser> _userManager;

        public ReviewTransfersModel(ITransferRequestService transferService, UserManager<ApplicationUser> userManager)
        {
            _transferService = transferService;
            _userManager = userManager;
        }

        public List<TransferRequestViewModel> PendingRequests { get; set; } = new();

        public async Task OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            var branch = user?.Branch ?? "";
            PendingRequests = await _transferService.GetPendingRequestsForBranchManagerAsync(branch);
        }
    }
}