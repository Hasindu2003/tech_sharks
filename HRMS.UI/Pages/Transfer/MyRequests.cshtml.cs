using HRMS.Application.Models;
using HRMS.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HRMS.UI.Pages.Transfer
{
    [Authorize(Roles = "HR Manager,Area Manager,Branch Manager,Employee")]
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