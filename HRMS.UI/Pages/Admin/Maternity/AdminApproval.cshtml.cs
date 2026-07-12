using System.Collections.Generic;
using System.Threading.Tasks;
using HRMS.Domain.Entities.Leave;
using HRMS.Infrastructure.Persistence;
using HRMS.UI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HRMS.UI.Pages.Admin.Maternity
{
    [Authorize(Roles = "Branch Manager, Area Manager, HR Manager")]
    public class AdminApprovalModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly IMaternityLeaveService _maternityService;

        public AdminApprovalModel(ApplicationDbContext context, IMaternityLeaveService maternityService)
        {
            _context = context;
            _maternityService = maternityService;
        }

        public List<Leave> PendingApprovals { get; set; } = new();

        [TempData]
        public string? SuccessMessage { get; set; }

        [TempData]
        public string? ErrorMessage { get; set; }

        public async Task OnGetAsync()
        {
            PendingApprovals = await _maternityService.GetPendingAdminApprovalsAsync();
        }

        public async Task<IActionResult> OnPostApproveAsync(int leaveId, string comments)
        {
            try
            {
                await _maternityService.AdminApproveMaternityLeaveAsync(leaveId, comments, true);
                SuccessMessage = "Maternity leave approved successfully!";
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
                await _maternityService.AdminApproveMaternityLeaveAsync(leaveId, reason, false);
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
