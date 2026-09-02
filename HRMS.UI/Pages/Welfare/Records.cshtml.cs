using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HRMS.Domain.Entities.Core;
using HRMS.Domain.Entities.Welfare;
using HRMS.Infrastructure.Identity;
using HRMS.Infrastructure.Persistence;
using HRMS.UI.Pages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HRMS.UI.Pages.Welfare
{
    using Employee = HRMS.Domain.Entities.Core.Employee;

    [Authorize(Roles = "HR Manager,HR Officer,Admin,Welfare Manager")]
    public class RecordsModel : BasePageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;

        public RecordsModel(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
            : base(context)
        {
            _userManager = userManager;
        }

        public List<WelfareRequest> Requests { get; set; } = new();
        public List<Branch> ScopedBranches { get; set; } = new();
        public List<WelfareType> WelfareTypes { get; set; } = new();

        public decimal TotalDisbursedAmount { get; set; }
        public int TotalApprovedCount { get; set; }
        public int TotalPendingCount { get; set; }
        public int TotalRequestsCount { get; set; }
        public bool IsHROfficerScoped { get; set; }

        public async Task OnGetAsync()
        {
            await LoadCurrentUserAsync();

            var currentUser = await _userManager.GetUserAsync(User);
            List<int>? allowedBranchIds = null;

            if (User.IsInRole("Admin") || User.IsInRole("HR Manager") || User.IsInRole("Welfare Manager"))
            {
                // Full company-wide access to all branches
                allowedBranchIds = null;
                IsHROfficerScoped = false;
                ScopedBranches = await _db.Branches.OrderBy(b => b.Name).ToListAsync();
            }
            else if (User.IsInRole("HR Officer"))
            {
                IsHROfficerScoped = true;
                if (!string.IsNullOrEmpty(currentUser?.ManagedBranches))
                {
                    if (currentUser.ManagedBranches.Equals("All", StringComparison.OrdinalIgnoreCase))
                    {
                        allowedBranchIds = null;
                        ScopedBranches = await _db.Branches.OrderBy(b => b.Name).ToListAsync();
                    }
                    else
                    {
                        allowedBranchIds = currentUser.ManagedBranches
                            .Split(',', StringSplitOptions.RemoveEmptyEntries)
                            .Select(s => int.TryParse(s.Trim(), out var id) ? id : 0)
                            .Where(id => id > 0)
                            .ToList();

                        ScopedBranches = await _db.Branches
                            .Where(b => allowedBranchIds.Contains(b.Id))
                            .OrderBy(b => b.Name)
                            .ToListAsync();
                    }
                }
                else
                {
                    allowedBranchIds = new List<int> { -1 };
                    ScopedBranches = new List<Branch>();
                }
            }

            WelfareTypes = await _db.WelfareTypes.Where(t => t.IsActive).OrderBy(t => t.TypeName).ToListAsync();

            var query = _db.WelfareRequests
                .Include(r => r.WelfareType)
                .Include(r => r.Employee)
                    .ThenInclude(e => e!.Branch)
                .Include(r => r.Employee)
                    .ThenInclude(e => e!.Department)
                .Include(r => r.Documents)
                .Where(r => r.Employee != null && !r.Employee.NIC.StartsWith("DUTY") && r.Employee.NIC != "DUTY-ACC");

            if (allowedBranchIds != null)
            {
                query = query.Where(r => allowedBranchIds.Contains(r.Employee!.BranchId));
            }

            Requests = await query
                .OrderByDescending(r => r.RequestDate)
                .ThenByDescending(r => r.CreatedAt)
                .ToListAsync();

            TotalRequestsCount = Requests.Count;
            TotalApprovedCount = Requests.Count(r => r.CurrentStatus == "Approved" || r.CurrentStatus == "Disbursed" || r.CurrentStatus == "PaymentCompleted");
            TotalPendingCount = Requests.Count(r => r.CurrentStatus == "Pending" || r.Status == "UnderReview" || r.CurrentStatus == "UnderReview");
            TotalDisbursedAmount = Requests
                .Where(r => r.CurrentStatus == "Approved" || r.CurrentStatus == "Disbursed" || r.CurrentStatus == "PaymentCompleted")
                .Sum(r => r.ApprovedAmount ?? r.RequestedAmount);
        }
    }
}
