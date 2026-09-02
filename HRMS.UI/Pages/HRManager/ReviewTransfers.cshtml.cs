using HRMS.Infrastructure.Identity;
using HRMS.Infrastructure.Persistence;
using HRMS.Application.Models;
using HRMS.Application.Services;
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

        public List<TransferRequestViewModel> FinalizationQueue { get; set; } = new();
        public List<TransferRequestViewModel> IncomingRequests { get; set; } = new();
        public List<TransferRequestViewModel> OutgoingRequests { get; set; } = new();
        public string HRBranch { get; set; } = string.Empty;

        public async Task<IActionResult> OnGetAsync()
        {
            var hrUser = await _userManager.GetUserAsync(User);
            if (hrUser == null && !string.IsNullOrEmpty(User.Identity?.Name))
            {
                hrUser = await _userManager.FindByNameAsync(User.Identity.Name) ?? await _userManager.FindByEmailAsync(User.Identity.Name);
            }
            if (hrUser == null) return Challenge();

            HRBranch = !string.IsNullOrWhiteSpace(hrUser.Branch) ? hrUser.Branch : "Assigned Branches";

            var allFinalization = await _transferService.GetRequestsForHRFinalizationAsync();
            var allRequests = await _transferService.GetAllRequestsAsync();

            var assignedBranchNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (!string.IsNullOrWhiteSpace(hrUser.ManagedBranches))
            {
                var branchIds = hrUser.ManagedBranches
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => int.TryParse(s.Trim(), out int id) ? id : 0)
                    .Where(id => id > 0)
                    .ToList();

                if (branchIds.Any())
                {
                    var names = await _context.Branches
                        .Where(b => branchIds.Contains(b.Id))
                        .Select(b => b.Name)
                        .ToListAsync();

                    foreach (var name in names) assignedBranchNames.Add(name);
                }
            }

            if (!string.IsNullOrWhiteSpace(hrUser.Branch) && hrUser.Branch != "Multiple")
            {
                assignedBranchNames.Add(hrUser.Branch);
            }

            if (User.IsInRole("HR Manager") || !assignedBranchNames.Any())
            {
                FinalizationQueue = allFinalization;
                IncomingRequests = allRequests.Where(r => !string.IsNullOrEmpty(hrUser.Branch) && r.RequestedBranch == hrUser.Branch).ToList();
                OutgoingRequests = allRequests.Where(r => !string.IsNullOrEmpty(hrUser.Branch) && r.CurrentBranch == hrUser.Branch).ToList();
            }
            else
            {
                // Current Branch HR Officer finalizes the process
                FinalizationQueue = allFinalization
                    .Where(r => assignedBranchNames.Contains(r.CurrentBranch))
                    .ToList();

                IncomingRequests = allRequests
                    .Where(r => assignedBranchNames.Contains(r.RequestedBranch))
                    .ToList();

                OutgoingRequests = allRequests
                    .Where(r => assignedBranchNames.Contains(r.CurrentBranch))
                    .ToList();
            }

            return Page();
        }
    }
}
