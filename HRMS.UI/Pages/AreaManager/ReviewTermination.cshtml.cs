using System;
using System.Linq;
using System.Threading.Tasks;
using HRMS.Application.Models;
using HRMS.Application.Services;
using HRMS.Infrastructure.Identity;
using HRMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HRMS.UI.Pages.AreaManager
{
    [Authorize(Roles = "Area Manager")]
    public class ReviewTerminationModel : PageModel
    {
        private readonly ITerminationService _terminationService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;

        public ReviewTerminationModel(
            ITerminationService terminationService,
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext context)
        {
            _terminationService = terminationService;
            _userManager = userManager;
            _context = context;
        }

        public TerminationRequestViewModel? TerminationRequest { get; set; }
        public bool CanReview { get; set; }

        public async Task<IActionResult> OnGetAsync(int id)
        {
            TerminationRequest = await _terminationService.GetTerminationByIdAsync(id);
            if (TerminationRequest == null) return NotFound();

            CanReview = TerminationRequest.Status == TerminationStatusEnum.BMApproved;

            return Page();
        }

        public async Task<IActionResult> OnPostAsync(int id, string action, string comments)
        {
            if (string.IsNullOrWhiteSpace(comments) || comments.Trim().Length < 5)
            {
                TempData["ErrorMessage"] = "Comments are required (minimum 5 characters).";
                return RedirectToPage(new { id });
            }

            var user = await ResolveCurrentUserAsync();
            if (user == null) return Challenge();

            var req = await _terminationService.GetTerminationByIdAsync(id);
            if (req == null) return NotFound();

            bool isApprove = string.Equals(action, "approve", StringComparison.OrdinalIgnoreCase);

            var result = await _terminationService.AreaManagerReviewAsync(
                id,
                isApprove,
                comments.Trim(),
                user.Email ?? user.UserName ?? "Area Manager"
            );

            if (result.Success)
            {
                TempData["SuccessMessage"] = isApprove
                    ? $"Termination request for {req.EmployeeName} has been approved and sent to HR for finalization."
                    : $"Termination request for {req.EmployeeName} has been rejected.";

                return RedirectToPage("/AreaManager/ReviewTerminations");
            }

            TempData["ErrorMessage"] = result.ErrorMessage ?? "Failed to submit review.";
            return RedirectToPage(new { id });
        }

        private async Task<ApplicationUser?> ResolveCurrentUserAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user != null) return user;

            var username = User.Identity?.Name;
            if (!string.IsNullOrEmpty(username))
            {
                user = await _userManager.FindByNameAsync(username) ??
                       await _userManager.FindByEmailAsync(username);
            }
            return user;
        }
    }
}
