using System;
using System.Collections.Generic;
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
    public class ReviewTerminationsModel : PageModel
    {
        private readonly ITerminationService _terminationService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;

        public ReviewTerminationsModel(
            ITerminationService terminationService,
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext context)
        {
            _terminationService = terminationService;
            _userManager = userManager;
            _context = context;
        }

        public List<TerminationRequestViewModel> PendingRequests { get; set; } = new();
        public List<TerminationRequestViewModel> ReviewedRequests { get; set; } = new();
        public List<int> ManagedBranchIds { get; set; } = new();
        public string ManagerBranch { get; set; } = string.Empty;

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await ResolveCurrentUserAsync();
            if (user == null) return Challenge();

            await ResolveAreaScopeAsync(user);

            PendingRequests = await _terminationService.GetPendingForAreaManagerAsync(ManagedBranchIds, ManagerBranch);
            ReviewedRequests = await _terminationService.GetReviewedByAreaManagerAsync(ManagedBranchIds, ManagerBranch);

            return Page();
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

        private async Task ResolveAreaScopeAsync(ApplicationUser? user)
        {
            if (user == null) return;

            ManagerBranch = user.Branch ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(user.ManagedBranches))
            {
                ManagedBranchIds = user.ManagedBranches
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => int.TryParse(s.Trim(), out var id) ? id : 0)
                    .Where(id => id > 0)
                    .ToList();
            }
        }
    }
}
