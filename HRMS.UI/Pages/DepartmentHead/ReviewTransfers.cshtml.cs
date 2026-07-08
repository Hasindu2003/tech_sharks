using HRMS.Infrastructure.Identity;
using HRMS.Application.Models;
using HRMS.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HRMS.UI.Pages.DepartmentHead
{
    [Authorize(Roles = "Department Head")]
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
        public List<TransferRequestViewModel> ReviewedRequests { get; set; } = new();
        public string DeptHeadBranch { get; set; } = string.Empty;
        public string DeptHeadDepartment { get; set; } = string.Empty;

        public async Task OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            DeptHeadBranch = user?.Branch ?? "";
            DeptHeadDepartment = user?.Department ?? "";
            PendingRequests  = await _transferService.GetRequestsForDeptHeadAsync(DeptHeadBranch, DeptHeadDepartment);
            ReviewedRequests = await _transferService.GetReviewedByDeptHeadAsync(DeptHeadBranch, DeptHeadDepartment);
        }
    }
}
