using HRMS.Infrastructure.Identity;
using HRMS.UI.Models;
using HRMS.UI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HRMS.UI.Pages.Transfer
{
    [Authorize(Roles = "Employee")]
    public class SeparationModel : PageModel
    {
        private readonly ITransferRequestService _transferService;
        private readonly UserManager<ApplicationUser> _userManager;

        public SeparationModel(ITransferRequestService transferService, UserManager<ApplicationUser> userManager)
        {
            _transferService = transferService;
            _userManager = userManager;
        }

        public List<TransferRequestViewModel> MyRequests { get; set; } = new();
        public int TransferCount { get; set; }

        public async Task OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user != null)
            {
                MyRequests = await _transferService.GetRequestsByUserAsync(user.Email!);
                TransferCount = MyRequests.Count;
            }
        }
    }
}
