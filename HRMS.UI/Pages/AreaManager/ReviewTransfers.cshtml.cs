using HRMS.Application.Models;
using HRMS.Application.Services;
using HRMS.Infrastructure.Identity;
using HRMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HRMS.UI.Pages.AreaManager
{
    [Authorize(Roles = "Area Manager")]
    public class ReviewTransfersModel : PageModel
    {
        private readonly ITransferRequestService _transferService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;

        public ReviewTransfersModel(
            ITransferRequestService transferService,
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext context)
        {
            _transferService = transferService;
            _userManager = userManager;
            _context = context;
        }

        public List<TransferRequestViewModel> PendingRequests  { get; set; } = new();
        public List<TransferRequestViewModel> ReviewedRequests { get; set; } = new();

        public async Task OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null && !string.IsNullOrEmpty(User.Identity?.Name))
            {
                user = await _userManager.FindByNameAsync(User.Identity.Name) ?? await _userManager.FindByEmailAsync(User.Identity.Name);
            }

            var allPending  = await _transferService.GetRequestsForAreaManagerAsync();
            var allReviewed = await _transferService.GetReviewedByAreaManagerAsync();

            if (user != null && !string.IsNullOrWhiteSpace(user.ManagedBranches))
            {
                var branchIds = user.ManagedBranches
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => int.TryParse(s.Trim(), out int id) ? id : 0)
                    .Where(id => id > 0)
                    .ToList();

                if (branchIds.Any())
                {
                    var managedBranchNames = await _context.Branches
                        .Where(b => branchIds.Contains(b.Id))
                        .Select(b => b.Name)
                        .ToListAsync();

                    PendingRequests = allPending
                        .Where(r => managedBranchNames.Contains(r.CurrentBranch))
                        .ToList();

                    ReviewedRequests = allReviewed
                        .Where(r => managedBranchNames.Contains(r.CurrentBranch))
                        .ToList();

                    return;
                }
            }

            PendingRequests  = allPending;
            ReviewedRequests = allReviewed;
        }
    }
}
