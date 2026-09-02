using HRMS.Domain.Entities.Welfare;
using HRMS.Infrastructure.Identity;
using HRMS.Infrastructure.Persistence;
using HRMS.UI.Pages;
using HRMS.Application.Services;
using HRMS.Domain.Entities.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HRMS.UI.Pages.Welfare.Approvals
{
    [Authorize(Roles = "Welfare Manager,Department Head,HR Manager,Admin")]
    public class DepartmentHeadApprovalModel : BasePageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly INotificationService _notifService;

        public DepartmentHeadApprovalModel(ApplicationDbContext context, UserManager<ApplicationUser> userManager, INotificationService notifService)
            : base(context)
        {
            _userManager = userManager;
            _notifService = notifService;
        }

        public List<WelfareRequest> AllRequests { get; set; } = new();
        public List<WelfareRequest> PendingRequests { get; set; } = new();
        public List<WelfareType> WelfareTypes { get; set; } = new();
        
        public int PendingCount { get; set; }
        public int ApprovedCount { get; set; }
        public int RejectedCount { get; set; }
        public int TotalProcessedCount { get; set; }
        public decimal TotalPendingAmount { get; set; }

        [BindProperty] public int RequestId { get; set; }
        [BindProperty] public string Action { get; set; } = string.Empty;
        [BindProperty] public string? Comments { get; set; }

        public async Task OnGetAsync()
        {
            await LoadCurrentUserAsync();

            WelfareTypes = await _db.WelfareTypes.OrderBy(t => t.TypeName).ToListAsync();

            AllRequests = await _db.WelfareRequests
                .Include(r => r.WelfareType)
                .Include(r => r.Employee)
                    .ThenInclude(e => e.Department)
                .Include(r => r.Employee)
                    .ThenInclude(e => e.Branch)
                .Include(r => r.Documents)
                .Where(r => r.Employee != null && !r.Employee.NIC.StartsWith("DUTY") && r.Employee.NIC != "DUTY-ACC" && !r.IsDraft && r.Status != "Draft" && r.CurrentStatus != "Draft")
                .ToListAsync();

            PendingRequests = AllRequests
                .Where(r => r.CurrentLevel == "DepartmentHead" && (r.CurrentStatus == "Pending" || r.CurrentStatus == "UnderReview"))
                .OrderBy(r => WelfarePayrollHelper.GetUrgencyScore(r.Remark))
                .ThenByDescending(r => r.CreatedAt)
                .ToList();

            AllRequests = AllRequests
                .OrderBy(r => r.CurrentLevel == "DepartmentHead" && (r.CurrentStatus == "Pending" || r.CurrentStatus == "UnderReview") ? 0 : 1)
                .ThenBy(r => WelfarePayrollHelper.GetUrgencyScore(r.Remark))
                .ThenByDescending(r => r.CreatedAt)
                .ToList();

            PendingCount = PendingRequests.Count;
            TotalPendingAmount = PendingRequests.Sum(r => r.RequestedAmount);

            ApprovedCount = AllRequests.Count(r => 
                r.Status == "Approved" || r.Status == "Paid" || r.Status == "PaymentCompleted" || r.Status == "Disbursed" ||
                r.CurrentStatus == "Approved" || r.CurrentStatus == "PendingPayment" || r.CurrentStatus == "PaymentCompleted" ||
                r.CurrentStatus == "Paid" || r.CurrentStatus == "Disbursed");

            RejectedCount = AllRequests.Count(r => 
                r.Status == "Rejected" || r.CurrentStatus == "Rejected");

            TotalProcessedCount = ApprovedCount + RejectedCount;
        }

        public async Task<IActionResult> OnPostAsync()
        {
            await LoadCurrentUserAsync();

            var request = await _db.WelfareRequests
                .Include(r => r.Employee)
                .Include(r => r.WelfareType)
                .FirstOrDefaultAsync(r => r.RequestId == RequestId && r.Employee != null && !r.Employee.NIC.StartsWith("DUTY") && r.Employee.NIC != "DUTY-ACC");

            if (request == null)
            {
                TempData["Error"] = "Welfare request not found.";
                return RedirectToPage();
            }

            var user = await _userManager.GetUserAsync(User);
            var employee = await _db.Employees
                .FirstOrDefaultAsync(e => e.Email == (user != null ? user.Email : "") && !e.NIC.StartsWith("DUTY") && e.NIC != "DUTY-ACC");

            var approval = new WelfareApproval
            {
                RequestId = RequestId,
                ApproverId = employee?.Id ?? 0,
                ApproverLevel = "DepartmentHead",
                Action = Action,
                Comments = Comments,
                ActionDate = DateTime.Now
            };

            if (Action == "Approved")
            {
                request.CurrentLevel = "HRManager";
                request.CurrentStatus = "PendingPayment";
                request.Status = "Approved";
                request.ApprovedAmount = request.ApprovedAmount ?? request.RequestedAmount;

                _db.WelfareApprovals.Add(approval);
                await _db.SaveChangesAsync();

                // 1. Notify Applicant Employee
                if (request.Employee != null && !string.IsNullOrEmpty(request.Employee.Email))
                {
                    try
                    {
                        await _notifService.CreateNotificationAsync(
                            request.Employee.Email,
                            "Welfare Request Approved",
                            $"Your welfare request (WF-{request.RequestId:D4}) of LKR {request.ApprovedAmount.Value:N2} has been approved by the Welfare Department Head and forwarded to HR for payment.",
                            CoreNotificationType.Approved,
                            "/Welfare/StatusTracking?id=" + request.RequestId
                        );
                    }
                    catch { }
                }

                // 2. Notify HR Manager
                var hrManagers = await _userManager.GetUsersInRoleAsync("HR Manager");
                foreach (var hrm in hrManagers)
                {
                    if (!string.IsNullOrEmpty(hrm.Email))
                    {
                        try
                        {
                            await _notifService.CreateNotificationAsync(
                                hrm.Email,
                                "Welfare Payment Pending",
                                $"Welfare request WF-{request.RequestId:D4} (LKR {request.ApprovedAmount.Value:N2}) for {request.Employee?.FullName} has been approved by the Welfare Department Head and is awaiting payment.",
                                CoreNotificationType.Info,
                                "/Welfare/Payments"
                            );
                        }
                        catch { }
                    }
                }

                // 3. Notify HR Officers assigned to this employee's branch
                if (request.Employee != null && request.Employee.BranchId > 0)
                {
                    var branchIdStr = request.Employee.BranchId.ToString();
                    var hrOfficers = await _userManager.GetUsersInRoleAsync("HR Officer");
                    foreach (var hro in hrOfficers)
                    {
                        if (!string.IsNullOrEmpty(hro.Email) && !string.IsNullOrEmpty(hro.ManagedBranches))
                        {
                            var assignedBranchIds = hro.ManagedBranches
                                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                                .Select(s => s.Trim())
                                .ToList();

                            if (assignedBranchIds.Contains(branchIdStr) || hro.ManagedBranches.Equals("All", StringComparison.OrdinalIgnoreCase))
                            {
                                try
                                {
                                    await _notifService.CreateNotificationAsync(
                                        hro.Email,
                                        "Welfare Payment Pending",
                                        $"Welfare request WF-{request.RequestId:D4} (LKR {request.ApprovedAmount.Value:N2}) for {request.Employee?.FullName} has been approved by the Welfare Department Head and is awaiting payment.",
                                        CoreNotificationType.Info,
                                        "/Welfare/Payments"
                                    );
                                }
                                catch { }
                            }
                        }
                    }
                }

                TempData["Success"] = $"Request WF-{request.RequestId:D4} has been approved and forwarded to HR for payment.";
            }
            else if (Action == "Rejected")
            {
                request.CurrentStatus = "Rejected";
                request.Status = "Rejected";

                _db.WelfareApprovals.Add(approval);
                await _db.SaveChangesAsync();

                // Notify request owner
                if (request.Employee != null && !string.IsNullOrEmpty(request.Employee.Email))
                {
                    try
                    {
                        await _notifService.CreateNotificationAsync(
                            request.Employee.Email,
                            "Welfare Request Rejected",
                            $"Your welfare request (WF-{request.RequestId:D4}) has been rejected by the Welfare Manager. Remark: {Comments ?? "No comments provided."}",
                            CoreNotificationType.Rejected,
                            "/Welfare/StatusTracking?id=" + request.RequestId
                        );
                    }
                    catch { }
                }

                TempData["Success"] = $"Request WF-{request.RequestId:D4} has been rejected.";
            }

            return RedirectToPage();
        }
    }
}
