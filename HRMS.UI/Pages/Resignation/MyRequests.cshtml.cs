using HRMS.Infrastructure.Identity;
using HRMS.Application.Models;
using HRMS.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HRMS.UI.Pages.Resignation
{
    [Authorize(Roles = "Employee")]
    public class MyRequestsModel : PageModel
    {
        private readonly IResignationService _resignationService;
        private readonly UserManager<ApplicationUser> _userManager;

        public MyRequestsModel(IResignationService resignationService, UserManager<ApplicationUser> userManager)
        {
            _resignationService = resignationService;
            _userManager = userManager;
        }

        public List<ResignationRequestViewModel> Requests { get; set; } = new();

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            Requests = await _resignationService.GetMyResignationsAsync(user.Email!);
            return Page();
        }

        public async Task<IActionResult> OnPostDeleteDraftAsync(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var identifier = user.Email ?? user.UserName ?? "";
            (bool success, string? error) = await _resignationService.DeleteDraftAsync(id, identifier);
            if (!success)
            {
                TempData["ErrorMessage"] = error;
            }
            else
            {
                TempData["SuccessMessage"] = "Draft resignation was deleted successfully.";
            }

            return RedirectToPage("/Resignation/MyRequests");
        }
    }
}
