using HRMS.Infrastructure.Identity;
using HRMS.Application.Models;
using HRMS.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Claims;

namespace HRMS.UI.Pages.HRManager
{
    [Authorize(Roles = "HR Manager,HR Officer")]
    public class ReviewDeathModel : PageModel
    {
        private readonly IDeathService _deathService;
        private readonly UserManager<ApplicationUser> _userManager;

        public ReviewDeathModel(IDeathService deathService, UserManager<ApplicationUser> userManager)
        {
            _deathService = deathService;
            _userManager = userManager;
        }

        public DeathRequestViewModel? RequestModel { get; set; }

        public async Task<IActionResult> OnGetAsync(int id)
        {
            RequestModel = await _deathService.GetByIdAsync(id);
            if (RequestModel == null) return NotFound();
            return Page();
        }

        public async Task<IActionResult> OnPostAsync(int id, string action, string comments)
        {
            if (string.IsNullOrWhiteSpace(comments))
            {
                TempData["ErrorMessage"] = "Comments are required for HR finalization.";
                return RedirectToPage(new { id });
            }

            var email = User.FindFirstValue(ClaimTypes.Email) ?? User.Identity?.Name ?? "HR";
            bool success = action == "approve" 
                ? await _deathService.HRManagerApproveAsync(id, comments, email, _userManager)
                : await _deathService.HRManagerRejectAsync(id, comments, email);

            TempData[success ? "SuccessMessage" : "ErrorMessage"] = success 
                ? $"Death process for employee {(action == "approve" ? "finalized, account deactivated, and system closure completed successfully" : "rejected")}." 
                : "Failed to process request.";
                
            return RedirectToPage("/Separation/Dashboard", new { ActiveTab = "Death" });
        }

        public async Task<IActionResult> OnPostProcessClosureAsync(int id)
        {
            var email = User.FindFirstValue(ClaimTypes.Email) ?? User.Identity?.Name ?? "HR";
            var (success, msg) = await _deathService.ProcessClosureAsync(id, email, _userManager);

            if (success)
            {
                TempData["SuccessMessage"] = "Employee account deactivated, payroll stopped, and finance process triggered. Final closure completed.";
            }
            else
            {
                TempData["ErrorMessage"] = "Closure failed: " + msg;
            }

            return RedirectToPage("/Separation/Dashboard", new { ActiveTab = "Death" });
        }
    }
}
