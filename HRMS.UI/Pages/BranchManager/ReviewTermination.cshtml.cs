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

namespace HRMS.UI.Pages.BranchManager
{
    [Authorize(Roles = "Branch Manager")]
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
        public string ManagerBranch { get; set; } = string.Empty;
        public bool CanReview { get; set; }

        public async Task<IActionResult> OnGetAsync(int id)
        {
            var user = await ResolveCurrentUserAsync();
            await ResolveManagerBranchAsync(user);

            TerminationRequest = await _terminationService.GetTerminationByIdAsync(id);
            if (TerminationRequest == null) return NotFound();

            CanReview = TerminationRequest.Status == TerminationStatusEnum.DeptHeadsApproved;

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

            var result = await _terminationService.BranchManagerReviewAsync(
                id,
                isApprove,
                comments.Trim(),
                user.Email ?? user.UserName ?? "Branch Manager"
            );

            if (result.Success)
            {
                TempData["SuccessMessage"] = isApprove
                    ? $"Termination request for {req.EmployeeName} has been approved and forwarded to Area Manager."
                    : $"Termination request for {req.EmployeeName} has been rejected.";

                return RedirectToPage("/BranchManager/ReviewTerminations");
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

        private async Task ResolveManagerBranchAsync(ApplicationUser? user)
        {
            if (user == null) return;

            ManagerBranch = user.Branch ?? string.Empty;

            if (user.EmployeeId.HasValue && user.EmployeeId.Value > 0)
            {
                var emp = await _context.Employees
                    .Include(e => e.Branch)
                    .FirstOrDefaultAsync(e => e.Id == user.EmployeeId.Value);

                if (emp?.Branch != null && string.IsNullOrWhiteSpace(ManagerBranch))
                {
                    ManagerBranch = emp.Branch.Name;
                }
            }
        }
    }
}
