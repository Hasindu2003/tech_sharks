using System.Collections.Generic;
using System.Threading.Tasks;
using HRMS.Domain.Entities.Leave;
using HRMS.Infrastructure.Persistence;
using HRMS.UI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HRMS.UI.Pages.Admin.Overseas
{
    [Authorize(Roles = "HR Manager, Area Manager")]
    public class BoardApprovalModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly IOverseasLeaveService _overseasService;

        public BoardApprovalModel(ApplicationDbContext context, IOverseasLeaveService overseasService)
        {
            _context = context;
            _overseasService = overseasService;
        }

        public List<Leave> PendingApprovals { get; set; } = new();

        [TempData]
        public string? SuccessMessage { get; set; }

        [TempData]
        public string? ErrorMessage { get; set; }

        public async Task OnGetAsync()
        {
            PendingApprovals = await _overseasService.GetPendingBoardApprovalsAsync();
        }

        public async Task<IActionResult> OnPostApproveAsync(int leaveId, string comments)
        {
            try
            {
                await _overseasService.BoardApproveOverseasLeaveAsync(leaveId, comments, true);
                SuccessMessage = "Overseas leave approved successfully!";
            }
            catch (System.Exception ex)
            {
                ErrorMessage = ex.Message;
            }
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostRejectAsync(int leaveId, string reason)
        {
            try
            {
                await _overseasService.BoardApproveOverseasLeaveAsync(leaveId, reason, false);
                SuccessMessage = "Overseas leave rejected successfully!";
            }
            catch (System.Exception ex)
            {
                ErrorMessage = ex.Message;
            }
            return RedirectToPage();
        }
    }
}
