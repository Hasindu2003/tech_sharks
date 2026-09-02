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

namespace HRMS.UI.Pages.HRManager
{
    [Authorize(Roles = "HR Manager,HR Officer")]
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
        public List<ResignationRequestViewModel> ApprovedRequests { get; set; } = new();

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await ResolveCurrentUserAsync();
            List<int>? managedBranchIds = null;

            if (user != null && User.IsInRole("HR Officer") && !User.IsInRole("HR Manager"))
            {
                if (!string.IsNullOrWhiteSpace(user.ManagedBranches))
                {
                    managedBranchIds = user.ManagedBranches.Split(',')
                        .Select(s => int.TryParse(s.Trim(), out var id) ? id : 0)
                        .Where(id => id > 0)
                        .ToList();
                }
                else if (!string.IsNullOrWhiteSpace(user.Branch))
                {
                    var b = await _context.Branches.FirstOrDefaultAsync(x => x.Name.ToLower() == user.Branch.Trim().ToLower());
                    if (b != null) managedBranchIds = new List<int> { b.Id };
                }
            }

            PendingRequests = await _resignationService.GetPendingForHRManagerAsync(managedBranchIds);

            // Also show all HR-approved ones so HR can process effective dates & reactivate
            var all = await _resignationService.GetAllAsync();
            if (managedBranchIds != null && managedBranchIds.Any())
            {
                var branchNames = await _context.Branches
                    .Where(b => managedBranchIds.Contains(b.Id))
                    .Select(b => b.Name.ToLower())
                    .ToListAsync();
                all = all.Where(r => r.Branch != null && branchNames.Contains(r.Branch.ToLower())).ToList();
            }

            ApprovedRequests = all.Where(r =>
                r.Status == ResignationStatusEnum.HRApproved ||
                r.Status == ResignationStatusEnum.Completed).ToList();

            return Page();
        }

        public async Task<IActionResult> OnPostApproveAsync(int id, string comments)
        {
            if (string.IsNullOrWhiteSpace(comments))
            {
                TempData["ErrorMessage"] = "Comments are required for final approval.";
                return RedirectToPage();
            }

            var user = await ResolveCurrentUserAsync();
            if (user == null) return Challenge();

            var success = await _resignationService.HRManagerApproveAsync(id, comments, user.Email!);
            TempData[success ? "SuccessMessage" : "ErrorMessage"] =
                success ? "Resignation officially approved. Acceptance letter generated and employee notified." : "Unable to process.";

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

            var success = await _resignationService.HRManagerRejectAsync(id, comments, user.Email!);
            TempData[success ? "SuccessMessage" : "ErrorMessage"] =
                success ? "Resignation request has been rejected." : "Unable to process.";

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostProcessEffectiveDateAsync(int id)
        {
            var user = await ResolveCurrentUserAsync();
            if (user == null) return Challenge();

            var (success, error) = await _resignationService.ProcessEffectiveDateAsync(id, user.Email!, _userManager);
            TempData[success ? "SuccessMessage" : "ErrorMessage"] =
                success ? "Account has been deactivated. Separation completed." : error;

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostReactivateAsync(int id)
        {
            var user = await ResolveCurrentUserAsync();
            if (user == null) return Challenge();

            var (success, error) = await _resignationService.ReactivateAccountAsync(id, user.Email!, _userManager);
            TempData[success ? "SuccessMessage" : "ErrorMessage"] =
                success ? "Account has been reactivated successfully." : error;

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
