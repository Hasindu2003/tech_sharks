using HRMS.Infrastructure.Identity;
using HRMS.UI.Models;
using HRMS.UI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HRMS.UI.Pages
{
    [Authorize]
    public class IndexModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ITransferRequestService _transferService;

        public IndexModel(UserManager<ApplicationUser> userManager, ITransferRequestService transferService)
        {
            _userManager = userManager;
            _transferService = transferService;
        }

        public string UserRole { get; set; } = "Guest";
        public string FullName { get; set; } = "User";
        public string Greeting { get; set; } = "Good Morning";
        public List<TransferRequestViewModel> PendingRequests { get; set; } = new();
        public int PendingCount { get; set; }

        public async Task OnGetAsync()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                var user = await _userManager.GetUserAsync(User);
                if (user != null)
                {
                    FullName = user.FullName;
                }

                var hour = DateTime.Now.Hour;
                Greeting = hour switch
                {
                    < 12 => "Good Morning",
                    < 17 => "Good Afternoon",
                    _ => "Good Evening"
                };

                if (User.IsInRole("Area Manager"))
                {
                    UserRole = "Area Manager";
                    PendingRequests = await _transferService.GetRequestsForAreaManagerAsync();
                }
                else if (User.IsInRole("HR Manager"))
                {
                    UserRole = "HR Manager";
                    PendingRequests = await _transferService.GetPendingRequestsForHRManagerAsync();
                }
                else if (User.IsInRole("Branch Manager"))
                {
                    UserRole = "Branch Manager";
                    var branch = user?.Branch ?? "";
                    PendingRequests = await _transferService.GetPendingRequestsForBranchManagerAsync(branch);
                }
                else if (User.IsInRole("Employee"))
                {
                    UserRole = "Employee";
                    PendingRequests = await _transferService.GetRequestsByUserAsync(User.Identity?.Name ?? "");
                }

                PendingCount = PendingRequests.Count;
            }
        }
    }
}
