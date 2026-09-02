using HRMS.Application.Models;
using HRMS.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HRMS.UI.Pages.Transfer
{
    [Authorize(Roles = "Employee")]
    /// <summary>
    /// Page model for the "My Requests" page.
    /// Retrieves and displays all transfer requests initiated by the logged-in user.
    /// </summary>
    public class MyRequestsModel : PageModel
    {
        private readonly ITransferRequestService _transferService;

        public MyRequestsModel(ITransferRequestService transferService)
        {
            _transferService = transferService;
        }

        public List<TransferRequestViewModel> Requests { get; set; } = new();

        public async Task OnGetAsync()
        {
            Requests = await _transferService.GetRequestsByUserAsync(User.Identity!.Name!);
        }
    }
}