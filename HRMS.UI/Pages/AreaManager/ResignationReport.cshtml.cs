using HRMS.Infrastructure.Identity;
using HRMS.Application.Models;
using HRMS.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HRMS.UI.Pages.AreaManager
{
    [Authorize(Roles = "Admin,Area Manager")]
    public class ResignationReportModel : PageModel
    {
        private readonly IResignationService _resignationService;
        private readonly UserManager<ApplicationUser> _userManager;

        public ResignationReportModel(IResignationService resignationService, UserManager<ApplicationUser> userManager)
        {
            _resignationService = resignationService;
            _userManager = userManager;
        }

        public List<ResignationRequestViewModel> CompletedResignations { get; set; } = new();

        public async Task<IActionResult> OnGetAsync()
        {
            var all = await _resignationService.GetAllAsync();
            CompletedResignations = all.Where(r =>
                r.Status == ResignationStatusEnum.Completed ||
                r.Status == ResignationStatusEnum.HRApproved).ToList();
            return Page();
        }

        public async Task<IActionResult> OnPostReactivateAsync(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var (success, error) = await _resignationService.ReactivateAccountAsync(id, user.Email!, _userManager);
            TempData[success ? "SuccessMessage" : "ErrorMessage"] =
                success ? "Employee account has been successfully reactivated." : error;

            return RedirectToPage();
        }
    }
}
