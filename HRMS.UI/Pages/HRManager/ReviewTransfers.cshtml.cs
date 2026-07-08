using HRMS.Infrastructure.Identity;
using HRMS.Application.Models;
using HRMS.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HRMS.UI.Pages.HRManager
{
    [Authorize(Roles = "Admin,HR Manager")]
    public class ReviewTransfersModel : PageModel
    {
        private readonly ITransferRequestService _transferService;

        public ReviewTransfersModel(ITransferRequestService transferService)
        {
            _transferService = transferService;
        }

        public List<TransferRequestViewModel> PendingRequests { get; set; } = new();

        public async Task OnGetAsync()
        {
            PendingRequests = await _transferService.GetPendingRequestsForHRManagerAsync();
        }
    }
}
