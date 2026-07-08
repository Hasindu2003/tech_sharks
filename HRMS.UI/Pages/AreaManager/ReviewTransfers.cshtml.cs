using HRMS.Application.Models;
using HRMS.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HRMS.UI.Pages.AreaManager
{
    [Authorize(Roles = "Area Manager")]
    public class ReviewTransfersModel : PageModel
    {
        private readonly ITransferRequestService _transferService;

        public ReviewTransfersModel(ITransferRequestService transferService)
        {
            _transferService = transferService;
        }

        public List<TransferRequestViewModel> PendingRequests  { get; set; } = new();
        public List<TransferRequestViewModel> ReviewedRequests { get; set; } = new();

        public async Task OnGetAsync()
        {
            PendingRequests  = await _transferService.GetRequestsForAreaManagerAsync();
            ReviewedRequests = await _transferService.GetReviewedByAreaManagerAsync();
        }
    }
}
