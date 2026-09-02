using HRMS.Infrastructure.Identity;
using HRMS.Application.Models;
using HRMS.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HRMS.UI.Pages.Resignation
{
    [Authorize]
    public class DetailsModel : PageModel
    {
        private readonly IResignationService _resignationService;
        private readonly UserManager<ApplicationUser> _userManager;

        public DetailsModel(IResignationService resignationService, UserManager<ApplicationUser> userManager)
        {
            _resignationService = resignationService;
            _userManager = userManager;
        }

        public new ResignationRequestViewModel? Request { get; set; }

        public async Task<IActionResult> OnGetAsync(int id)
        {
            Request = await _resignationService.GetByIdAsync(id);
            if (Request == null) return NotFound();

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            // Employees can only view their own
            if (User.IsInRole("Employee") &&
                !string.Equals(Request.EmployeeEmail?.Trim(), user.Email?.Trim(), StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(Request.EpfNumber?.Trim(), user.EpfNumber?.Trim(), StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(Request.InitiatedBy?.Trim(), user.UserName?.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return Forbid();
            }

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
                return RedirectToPage(new { id });
            }

            TempData["SuccessMessage"] = "Resignation draft has been deleted successfully.";
            return RedirectToPage("/Transfer/Separation", new { ActiveTab = "Resignation" });
        }
    }
}
