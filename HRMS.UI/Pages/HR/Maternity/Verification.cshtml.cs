using System.Collections.Generic;
using System.Threading.Tasks;
using HRMS.Domain.Entities.Leave;
using HRMS.Infrastructure.Persistence;
using HRMS.UI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HRMS.UI.Pages.HR.Maternity
{
    [Authorize(Roles = "HR Manager")]
    public class VerificationModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly IMaternityLeaveService _maternityService;

        public VerificationModel(ApplicationDbContext context, IMaternityLeaveService maternityService)
        {
            _context = context;
            _maternityService = maternityService;
        }

        public List<Leave> PendingVerifications { get; set; } = new();

        [TempData]
        public string? SuccessMessage { get; set; }

        [TempData]
        public string? ErrorMessage { get; set; }

        public async Task OnGetAsync()
        {
            PendingVerifications = await _maternityService.GetPendingHrVerificationsAsync();
        }

        public async Task<IActionResult> OnPostVerifyAsync(int leaveId, string comments)
        {
            try
            {
                await _maternityService.HrVerifyMaternityLeaveAsync(leaveId, comments, true);
                SuccessMessage = "Maternity leave verified successfully!";
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
                await _maternityService.HrVerifyMaternityLeaveAsync(leaveId, reason, false);
                SuccessMessage = "Maternity leave rejected successfully!";
            }
            catch (System.Exception ex)
            {
                ErrorMessage = ex.Message;
            }
            return RedirectToPage();
        }
    }
}
