using HRMS.Application.Models;
using HRMS.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HRMS.UI.Pages.Termination
{
    [Authorize(Roles = "HR Manager,Area Manager,Branch Manager")]
    public class ApprovalQueueModel : PageModel
    {
        private readonly ITerminationService _terminationService;

        public ApprovalQueueModel(ITerminationService terminationService)
        {
            _terminationService = terminationService;
        }

        public List<TerminationRequestViewModel> PendingRequests { get; set; } = new();

        public async Task OnGetAsync()
        {
            PendingRequests = await _terminationService.GetPendingApprovalsAsync();
        }
    }
}
