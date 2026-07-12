using System.Collections.Generic;
using System.Threading.Tasks;
using HRMS.Domain.Entities.Leave;
using HRMS.Infrastructure.Persistence;
using HRMS.UI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HRMS.UI.Pages.HR.Overseas
{
    [Authorize(Roles = "HR Manager")]
    public class VerificationModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly IOverseasLeaveService _overseasService;

        public VerificationModel(ApplicationDbContext context, IOverseasLeaveService overseasService)
        {
            _context = context;
            _overseasService = overseasService;
        }

        public List<Leave> PendingVerifications { get; set; } = new();

        [TempData]
        public string? SuccessMessage { get; set; }

        [TempData]
        public string? ErrorMessage { get; set; }

        public async Task OnGetAsync()
        {
            PendingVerifications = await _overseasService.GetPendingVerificationsAsync();
        }

        public async Task<IActionResult> OnPostVerifyAsync(int leaveId, string comments)
        {
            try
            {
                await _overseasService.VerifyOverseasLeaveAsync(leaveId, comments, true);
                SuccessMessage = "Overseas leave verified and forwarded for board approval successfully!";
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
                await _overseasService.VerifyOverseasLeaveAsync(leaveId, reason, false);
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
