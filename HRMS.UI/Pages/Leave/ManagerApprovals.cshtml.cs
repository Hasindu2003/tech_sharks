using HRMS.Application.Leave;
using HRMS.Domain.Entities.Leave;
using HRMS.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HRMS.UI.Pages.Leave
{
    [Authorize]
    public class ManagerApprovalsModel : PageModel
    {
        private readonly ILeaveService _leaveService;
        private readonly UserManager<ApplicationUser> _userManager;

        public ManagerApprovalsModel(ILeaveService leaveService, UserManager<ApplicationUser> userManager)
        {
            _leaveService = leaveService;
            _userManager = userManager;
        }

        public bool HasEmployeeProfile { get; set; }
        public List<LeaveSummaryDto> PendingLeaves { get; set; } = new();

        public async Task OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user?.EmployeeId == null)
            {
                HasEmployeeProfile = false;
                return;
            }

            HasEmployeeProfile = true;
            PendingLeaves = await _leaveService.GetPendingForManagerAsync(user.EmployeeId.Value);
        }

        public async Task<IActionResult> OnPostActionAsync(int leaveId, string action, string? comments)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user?.EmployeeId == null)
                return Forbid();

            if (!Enum.TryParse<ApprovalAction>(action, out var approvalAction))
                return BadRequest();

            var result =
                await _leaveService.ManagerActionAsync(leaveId, user.EmployeeId.Value, approvalAction, comments);
            TempData[result.Success ? "SuccessMessage" : "ErrorMessage"] = result.Message;
            return RedirectToPage();
        }
    }
}
