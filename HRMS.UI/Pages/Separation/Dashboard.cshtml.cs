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

namespace HRMS.UI.Pages.Separation
{
    [Authorize(Roles = "Department Head,Branch Manager,Area Manager,HR Manager,HR Officer")]
    public class DashboardModel : PageModel
    {
        private readonly ITransferRequestService _transferService;
        private readonly IResignationService _resignationService;
        private readonly ITerminationService _terminationService;
        private readonly IDeathService _deathService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;

        public DashboardModel(
            ITransferRequestService transferService,
            IResignationService resignationService,
            ITerminationService terminationService,
            IDeathService deathService,
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext context)
        {
            _transferService = transferService;
            _resignationService = resignationService;
            _terminationService = terminationService;
            _deathService = deathService;
            _userManager = userManager;
            _context = context;
        }

        [BindProperty(SupportsGet = true)]
        public string ActiveTab { get; set; } = "Transfers";

        public string UserRole { get; set; } = string.Empty;
        public string ScopeSubtitle { get; set; } = string.Empty;
        public string UserBranch { get; set; } = string.Empty;
        public string UserDepartment { get; set; } = string.Empty;
        public List<int> ManagedBranchIds { get; set; } = new();

        // ── Available Tabs for current role ──
        public List<string> AvailableTabs { get; set; } = new();

        // ── Queues ──
        public List<TransferRequestViewModel> PendingTransfers { get; set; } = new();
        public List<TransferRequestViewModel> ReviewedTransfers { get; set; } = new();

        public List<ResignationRequestViewModel> PendingResignations { get; set; } = new();
        public List<ResignationRequestViewModel> ReviewedResignations { get; set; } = new();

        public List<TerminationRequestViewModel> PendingTerminations { get; set; } = new();
        public List<TerminationRequestViewModel> ReviewedTerminations { get; set; } = new();

        public List<DeathRequestViewModel> PendingDeathRequests { get; set; } = new();
        public List<DeathRequestViewModel> ReviewedDeathRequests { get; set; } = new();

        public async Task OnGetAsync()
        {
            var user = await ResolveCurrentUserAsync();
            if (user == null) return;

            UserBranch = user.Branch ?? "";
            UserDepartment = user.Department ?? "";

            if ((string.IsNullOrWhiteSpace(UserBranch) || string.IsNullOrWhiteSpace(UserDepartment)) && user.EmployeeId.HasValue)
            {
                var emp = await _context.Employees
                    .Include(e => e.Branch)
                    .Include(e => e.Department)
                    .FirstOrDefaultAsync(e => e.Id == user.EmployeeId.Value);

                if (emp != null)
                {
                    if (string.IsNullOrWhiteSpace(UserBranch)) UserBranch = emp.Branch?.Name ?? "";
                    if (string.IsNullOrWhiteSpace(UserDepartment)) UserDepartment = emp.Department?.Name ?? "";
                }
            }

            if (!string.IsNullOrWhiteSpace(user.ManagedBranches))
            {
                ManagedBranchIds = user.ManagedBranches
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => int.TryParse(s.Trim(), out var id) ? id : 0)
                    .Where(id => id > 0)
                    .ToList();
            }

            // ── Determine Role & Available Tabs ──
            if (User.IsInRole("Department Head"))
            {
                UserRole = "Department Head";
                ScopeSubtitle = $"{UserDepartment} Department — {UserBranch} Branch";
                AvailableTabs = new List<string> { "Transfers", "Resignations", "Terminations" };
            }
            else if (User.IsInRole("Branch Manager"))
            {
                UserRole = "Branch Manager";
                ScopeSubtitle = $"{UserBranch} Branch";
                AvailableTabs = new List<string> { "Transfers", "Resignations", "Terminations", "Death" };
            }
            else if (User.IsInRole("Area Manager"))
            {
                UserRole = "Area Manager";
                ScopeSubtitle = string.IsNullOrWhiteSpace(user.Branch) ? "Regional Oversight" : $"{user.Branch}";
                AvailableTabs = new List<string> { "Transfers", "Terminations", "Resignations", "Death" };
            }
            else if (User.IsInRole("HR Officer"))
            {
                UserRole = "HR Officer";
                ScopeSubtitle = "Human Resources — Regional Operations";
                AvailableTabs = new List<string> { "Transfers", "Terminations", "Resignations", "Death" };
            }
            else if (User.IsInRole("HR Manager"))
            {
                UserRole = "HR Manager";
                ScopeSubtitle = "Human Resources — Company-wide Oversight";
                AvailableTabs = new List<string> { "Transfers", "Terminations", "Resignations", "Death" };
            }

            // Ensure ActiveTab is valid for current role
            if (!AvailableTabs.Contains(ActiveTab, StringComparer.OrdinalIgnoreCase))
            {
                ActiveTab = AvailableTabs.FirstOrDefault() ?? "Transfers";
            }

            // ── Load Queues for current role ──
            await LoadQueuesForRoleAsync(user);
        }

        private async Task LoadQueuesForRoleAsync(ApplicationUser user)
        {
            bool isTransfersTab = ActiveTab.Equals("Transfers", StringComparison.OrdinalIgnoreCase);
            bool isTerminationsTab = ActiveTab.Equals("Terminations", StringComparison.OrdinalIgnoreCase);
            bool isResignationsTab = ActiveTab.Equals("Resignations", StringComparison.OrdinalIgnoreCase);
            bool isDeathTab = ActiveTab.Equals("Death", StringComparison.OrdinalIgnoreCase);

            if (User.IsInRole("Department Head"))
            {
                PendingTransfers = await _transferService.GetRequestsForDeptHeadAsync(UserBranch, UserDepartment);
                if (isTransfersTab) ReviewedTransfers = await _transferService.GetReviewedByDeptHeadAsync(UserBranch, UserDepartment);

                PendingResignations = await _resignationService.GetPendingForDeptHeadAsync(UserBranch, UserDepartment);
                if (isResignationsTab) ReviewedResignations = await _resignationService.GetReviewedByDeptHeadAsync(UserBranch, UserDepartment);

                PendingTerminations = await _terminationService.GetPendingForDeptHeadAsync(UserBranch, UserDepartment);
                if (isTerminationsTab) ReviewedTerminations = await _terminationService.GetReviewedByDeptHeadAsync(UserBranch, UserDepartment);
            }
            else if (User.IsInRole("Branch Manager"))
            {
                PendingTransfers = await _transferService.GetPendingRequestsForBranchManagerAsync(UserBranch);
                if (isTransfersTab) ReviewedTransfers = await _transferService.GetReviewedByBranchManagerAsync(UserBranch);

                PendingResignations = await _resignationService.GetPendingForBranchManagerAsync(UserBranch);
                if (isResignationsTab) ReviewedResignations = await _resignationService.GetReviewedByBranchManagerAsync(UserBranch);

                PendingTerminations = await _terminationService.GetPendingForBranchManagerAsync(UserBranch);
                if (isTerminationsTab) ReviewedTerminations = await _terminationService.GetReviewedByBranchManagerAsync(UserBranch);

                PendingDeathRequests = await _deathService.GetAllPendingForBMAsync(UserBranch);
                if (isDeathTab) ReviewedDeathRequests = await _deathService.GetReviewedForBMAsync(UserBranch);
            }
            else if (User.IsInRole("Area Manager"))
            {
                PendingTransfers = await _transferService.GetRequestsForAreaManagerAsync();
                if (isTransfersTab) ReviewedTransfers = await _transferService.GetReviewedByAreaManagerAsync();

                PendingTerminations = await _terminationService.GetPendingForAreaManagerAsync(ManagedBranchIds, UserBranch);
                if (isTerminationsTab) ReviewedTerminations = await _terminationService.GetReviewedByAreaManagerAsync(ManagedBranchIds, UserBranch);

                PendingResignations = await _resignationService.GetPendingForAreaManagerAsync(ManagedBranchIds, UserBranch);
                if (isResignationsTab) ReviewedResignations = await _resignationService.GetReviewedByAreaManagerAsync(ManagedBranchIds, UserBranch);

                PendingDeathRequests = await _deathService.GetAllPendingForAMAsync(ManagedBranchIds, UserBranch);
                if (isDeathTab) ReviewedDeathRequests = await _deathService.GetReviewedForAMAsync(ManagedBranchIds, UserBranch);
            }
            else if (User.IsInRole("HR Officer") || User.IsInRole("HR Manager"))
            {
                var managedIds = User.IsInRole("HR Officer") ? ManagedBranchIds : null;

                PendingTransfers = await _transferService.GetRequestsForHRFinalizationAsync();
                if (isTransfersTab) ReviewedTransfers = await _transferService.GetAllRequestsAsync();

                PendingTerminations = await _terminationService.GetPendingForHROfficerAsync(managedIds);
                if (isTerminationsTab) ReviewedTerminations = await _terminationService.GetReviewedByHROfficerAsync(managedIds);

                PendingResignations = await _resignationService.GetPendingForHRManagerAsync(managedIds);
                if (isResignationsTab) ReviewedResignations = await _resignationService.GetReviewedByHRManagerAsync(managedIds);

                PendingDeathRequests = await _deathService.GetAllPendingForHRAsync();
                if (isDeathTab) ReviewedDeathRequests = await _deathService.GetReviewedForHRAsync();
            }
        }

        private async Task<ApplicationUser?> ResolveCurrentUserAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user != null) return user;

            var username = User.Identity?.Name;
            if (!string.IsNullOrWhiteSpace(username))
            {
                user = await _userManager.FindByNameAsync(username) ?? await _userManager.FindByEmailAsync(username);
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
