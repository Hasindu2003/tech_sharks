using HRMS.Application.Models;
using HRMS.Application.Services;
using HRMS.Infrastructure.Identity;
using HRMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HRMS.UI.Pages.HRManager
{
    [Authorize(Roles = "HR Manager,HR Officer")]
    public class ReviewTransferModel : PageModel
    {
        private readonly ITransferRequestService _transferService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;

        public ReviewTransferModel(
            ITransferRequestService transferService,
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext context)
        {
            _transferService = transferService;
            _userManager = userManager;
            _context = context;
        }

        public TransferRequestViewModel? TransferRequest { get; set; }

        public async Task<IActionResult> OnGetAsync(int id)
        {
            TransferRequest = await _transferService.GetRequestByIdAsync(id);
            if (TransferRequest == null) return NotFound();
            return Page();
        }

        public async Task<IActionResult> OnPostAsync(int id, string action, string? comments = null)
        {
            var hrUser = await _userManager.GetUserAsync(User);
            if (hrUser == null && !string.IsNullOrEmpty(User.Identity?.Name))
            {
                hrUser = await _userManager.FindByNameAsync(User.Identity.Name) ?? await _userManager.FindByEmailAsync(User.Identity.Name);
            }

            var request = await _transferService.GetRequestByIdAsync(id);
            if (request == null) return NotFound();

            if (action == "mark_reviewed")
            {
                var remark = string.IsNullOrWhiteSpace(comments) ? "Seen and acknowledged by HR Manager" : comments.Trim();
                var ok = await _transferService.HRManagerMarkAsReviewedAsync(id, remark, hrUser?.Email ?? hrUser?.UserName ?? "");
                if (!ok)
                {
                    TempData["ErrorMessage"] = "Unable to mark as reviewed. The request may already be processed.";
                    return RedirectToPage("/Separation/Dashboard", new { ActiveTab = "Transfers" });
                }

                TempData["SuccessMessage"] = "Managerial transfer notice marked as reviewed successfully.";
                return RedirectToPage("/Separation/Dashboard", new { ActiveTab = "Transfers" });
            }

            if (string.IsNullOrWhiteSpace(comments) || comments.Trim().Length < 10)
            {
                ModelState.AddModelError(string.Empty, "Comments must be at least 10 characters.");
                TransferRequest = await _transferService.GetRequestByIdAsync(id);
                return Page();
            }

            // Validate that if caller is HR Officer, they are assigned to the Current Branch
            if (User.IsInRole("HR Officer") && !User.IsInRole("HR Manager"))
            {
                var assignedBranchNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (!string.IsNullOrWhiteSpace(hrUser?.ManagedBranches))
                {
                    var branchIds = hrUser.ManagedBranches
                        .Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(s => int.TryParse(s.Trim(), out int bId) ? bId : 0)
                        .Where(bId => bId > 0)
                        .ToList();

                    var names = await _context.Branches
                        .Where(b => branchIds.Contains(b.Id))
                        .Select(b => b.Name)
                        .ToListAsync();

                    foreach (var name in names) assignedBranchNames.Add(name);
                }
                if (!string.IsNullOrWhiteSpace(hrUser?.Branch) && hrUser.Branch != "Multiple")
                {
                    assignedBranchNames.Add(hrUser.Branch);
                }

                if (!assignedBranchNames.Contains(request.CurrentBranch))
                {
                    TempData["ErrorMessage"] = $"Only the HR Officer assigned to the employee's current branch ({request.CurrentBranch}) can finalize this transfer.";
                    return RedirectToPage("/HRManager/ReviewTransfers");
                }
            }

            bool approved = action == "approve";
            var okReview = await _transferService.HRManagerReviewAsync(id, approved, comments.Trim());

            if (!okReview)
            {
                TempData["ErrorMessage"] = "Unable to process review. The request may already be at a different stage.";
                return RedirectToPage("/HRManager/ReviewTransfers");
            }

            TempData["SuccessMessage"] = approved
                ? "Transfer request finalized and fully approved."
                : "Transfer request rejected at finalization.";

            return RedirectToPage("/HRManager/ReviewTransfers");
        }
    }
}
