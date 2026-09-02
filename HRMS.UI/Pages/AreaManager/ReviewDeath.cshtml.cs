using HRMS.Application.Models;
using HRMS.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Claims;

namespace HRMS.UI.Pages.AreaManager
{
    [Authorize(Roles = "Area Manager")]
    public class ReviewDeathModel : PageModel
    {
        private readonly IDeathService _deathService;

        public ReviewDeathModel(IDeathService deathService)
        {
            _deathService = deathService;
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
                TempData["ErrorMessage"] = "Comments are required to review.";
                return RedirectToPage(new { id });
            }

            var email = User.FindFirstValue(ClaimTypes.Email)!;
            bool success = action == "approve" 
                ? await _deathService.AMApproveAsync(id, comments, email)
                : await _deathService.AMRejectAsync(id, comments, email);

            TempData[success ? "SuccessMessage" : "ErrorMessage"] = success 
                ? $"Death process for {RequestModel?.EmployeeName ?? "employee"} {(action == "approve" ? "confirmed and forwarded to HR Manager" : "rejected")}." 
                : "Failed to process request.";
                
            return RedirectToPage("/Separation/Dashboard", new { ActiveTab = "Death" });
        }
    }
}
