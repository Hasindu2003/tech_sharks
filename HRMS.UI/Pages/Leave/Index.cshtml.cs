using HRMS.Application.Leave;
using HRMS.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HRMS.UI.Pages.Leave
{
    [Authorize]
    public class IndexModel : PageModel
    {
        private readonly ILeaveService _leaveService;
        private readonly UserManager<ApplicationUser> _userManager;

        public IndexModel(ILeaveService leaveService, UserManager<ApplicationUser> userManager)
        {
            _leaveService = leaveService;
            _userManager = userManager;
        }

        public bool HasEmployeeProfile { get; set; }
        public List<LeaveBalanceDto> Balances { get; set; } = new();
        public List<LeaveSummaryDto> MyLeaves { get; set; } = new();

        public async Task OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user?.EmployeeId == null)
            {
                HasEmployeeProfile = false;
                return;
            }

            HasEmployeeProfile = true;
            Balances = await _leaveService.GetBalancesAsync(user.EmployeeId.Value, DateTime.Today.Year);
            MyLeaves = await _leaveService.GetMyLeavesAsync(user.EmployeeId.Value);
        }

        public async Task<IActionResult> OnPostCancelAsync(int leaveId, string reason)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user?.EmployeeId == null)
                return Forbid();

            var result = await _leaveService.CancelAsync(leaveId, user.EmployeeId.Value, reason);
            TempData[result.Success ? "SuccessMessage" : "ErrorMessage"] = result.Message;
            return RedirectToPage();
        }
    }
}
