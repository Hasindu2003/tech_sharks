using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HRMS.Infrastructure.Identity;
using HRMS.Infrastructure.Persistence;
using HRMS.Application.Models;
using HRMS.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HRMS.UI.Pages.AreaManager
{
    [Authorize(Roles = "Area Manager")]
    public class ReviewResignationsModel : PageModel
    {
        private readonly IResignationService _resignationService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;

        public ReviewResignationsModel(
            IResignationService resignationService,
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext context)
        {
            _resignationService = resignationService;
            _userManager = userManager;
            _context = context;
        }

        public List<ResignationRequestViewModel> PendingRequests { get; set; } = new();
        public List<ResignationRequestViewModel> ReviewedRequests { get; set; } = new();

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await ResolveCurrentUserAsync();
            List<int>? managedBranchIds = null;
            string? branch = user?.Branch;

            if (user != null && !string.IsNullOrWhiteSpace(user.ManagedBranches))
            {
                managedBranchIds = user.ManagedBranches.Split(',')
                    .Select(s => int.TryParse(s.Trim(), out var id) ? id : 0)
                    .Where(id => id > 0)
                    .ToList();
            }

            PendingRequests = await _resignationService.GetPendingForAreaManagerAsync(managedBranchIds, branch);
            ReviewedRequests = await _resignationService.GetReviewedByAreaManagerAsync(managedBranchIds, branch);

            return Page();
        }

        public async Task<IActionResult> OnPostApproveAsync(int id, string comments)
        {
            if (string.IsNullOrWhiteSpace(comments))
            {
                TempData["ErrorMessage"] = "Comments are required when reviewing a resignation.";
                return RedirectToPage();
            }

            var user = await ResolveCurrentUserAsync();
            if (user == null) return Challenge();

            var success = await _resignationService.AreaManagerApproveAsync(id, comments, user.Email!);
            TempData[success ? "SuccessMessage" : "ErrorMessage"] =
                success ? "Resignation approved and forwarded to HR." : "Unable to process this request.";

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostRejectAsync(int id, string comments)
        {
            if (string.IsNullOrWhiteSpace(comments))
            {
                TempData["ErrorMessage"] = "A rejection reason is mandatory.";
                return RedirectToPage();
            }

            var user = await ResolveCurrentUserAsync();
            if (user == null) return Challenge();

            var success = await _resignationService.AreaManagerRejectAsync(id, comments, user.Email!);
            TempData[success ? "SuccessMessage" : "ErrorMessage"] =
                success ? "Resignation request has been rejected." : "Unable to process this request.";

            return RedirectToPage();
        }

        private async Task<ApplicationUser?> ResolveCurrentUserAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user != null) return user;

            if (!string.IsNullOrWhiteSpace(User.Identity?.Name))
            {
                user = await _userManager.FindByNameAsync(User.Identity.Name);
                if (user != null) return user;
            }

            var emailClaim = User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.Email)?.Value;
            if (!string.IsNullOrWhiteSpace(emailClaim))
            {
                user = await _userManager.FindByEmailAsync(emailClaim);
            }

            return user;
        }
    }
}
