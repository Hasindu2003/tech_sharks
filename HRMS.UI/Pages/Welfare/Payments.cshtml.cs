using HRMS.Application.Services;
using HRMS.Domain.Entities.Core;
using HRMS.Domain.Entities.Welfare;
using HRMS.Infrastructure.Identity;
using HRMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HRMS.UI.Pages.Welfare
{
    [Authorize(Roles = "HR Manager,HR Officer,Admin")]
    public class PaymentsModel : BasePageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly INotificationService _notifService;

        public PaymentsModel(ApplicationDbContext context, UserManager<ApplicationUser> userManager, INotificationService notifService)
            : base(context)
        {
            _userManager = userManager;
            _notifService = notifService;
        }

        public List<WelfareRequest> PendingPayments { get; set; } = new();
        public List<WelfareRequest> CompletedPayments { get; set; } = new();
        public List<WelfareType> WelfareTypes { get; set; } = new();

        public int PendingCount { get; set; }
        public decimal TotalPendingAmount { get; set; }
        public int PaidThisMonthCount { get; set; }
        public decimal PaidThisMonthAmount { get; set; }
        public int TotalProcessedCount { get; set; }

        [BindProperty] public int RequestId { get; set; }
        [BindProperty] public string Action { get; set; } = string.Empty;
        [BindProperty] public decimal? ApprovedAmount { get; set; }
        [BindProperty] public string? PaymentDate { get; set; }
        [BindProperty] public string? PaymentReference { get; set; }
        [BindProperty] public string? PaymentMethod { get; set; } = "Direct Bank Transfer";
        [BindProperty] public string? Comments { get; set; }

        public async Task OnGetAsync()
        {
            await LoadCurrentUserAsync();

            WelfareTypes = await _db.WelfareTypes.OrderBy(t => t.TypeName).ToListAsync();

            var query = _db.WelfareRequests
                .Include(r => r.WelfareType)
                .Include(r => r.Employee)
                    .ThenInclude(e => e.Department)
                .Include(r => r.Employee)
                    .ThenInclude(e => e.Branch)
                .Include(r => r.Employee)
                    .ThenInclude(e => e.Designation)
                .Include(r => r.Documents)
                .Where(r => r.Employee != null && !r.Employee.NIC.StartsWith("DUTY") && r.Employee.NIC != "DUTY-ACC");

            // Branch filtering for HR Officer
            if (User.IsInRole("HR Officer"))
            {
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser != null && !string.IsNullOrEmpty(currentUser.ManagedBranches) && !currentUser.ManagedBranches.Equals("All", StringComparison.OrdinalIgnoreCase))
                {
                    var assignedBranchIds = currentUser.ManagedBranches
                        .Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(s => int.TryParse(s.Trim(), out var id) ? id : 0)
                        .Where(id => id > 0)
                        .ToList();

                    if (assignedBranchIds.Any())
                    {
                        query = query.Where(r => assignedBranchIds.Contains(r.Employee.BranchId));
                    }
                }
            }

            var allRequests = await query.OrderByDescending(r => r.CreatedAt).ToListAsync();

            // Pending Payments: Approved by Welfare Department Head and awaiting HR disbursement
            PendingPayments = allRequests
                .Where(r => (r.CurrentLevel == "HRManager" && (r.CurrentStatus == "PendingPayment" || r.CurrentStatus == "Pending"))
                         || (r.Status == "Approved" && r.CurrentStatus != "PaymentCompleted" && r.CurrentStatus != "Paid" && r.CurrentStatus != "Rejected"))
                .ToList();

            // Completed Payments: Already disbursed
            CompletedPayments = allRequests
                .Where(r => r.CurrentStatus == "PaymentCompleted" || r.Status == "Paid")
                .ToList();

            PendingCount = PendingPayments.Count;
            TotalPendingAmount = PendingPayments.Sum(r => r.ApprovedAmount ?? r.RequestedAmount);

            var startOfMonth = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            var thisMonthPayments = CompletedPayments.Where(r => r.CreatedAt >= startOfMonth).ToList();
            PaidThisMonthCount = thisMonthPayments.Count;
            PaidThisMonthAmount = thisMonthPayments.Sum(r => r.ApprovedAmount ?? r.RequestedAmount);
            TotalProcessedCount = CompletedPayments.Count;
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

            var finalAmount = ApprovedAmount ?? request.ApprovedAmount ?? request.RequestedAmount;
            var payDateStr = string.IsNullOrWhiteSpace(PaymentDate) ? DateTime.Now.ToString("yyyy-MM-dd") : PaymentDate.Trim();
            var methodStr = string.IsNullOrWhiteSpace(PaymentMethod) ? "Direct Bank Transfer" : PaymentMethod.Trim();
            var refStr = string.IsNullOrWhiteSpace(PaymentReference) ? "N/A" : PaymentReference.Trim();

            var commentParts = new List<string>
            {
                $"Method: {methodStr}",
                $"Ref: {refStr}",
                $"Date: {payDateStr}"
            };
            if (!string.IsNullOrWhiteSpace(Comments))
            {
                commentParts.Add(Comments.Trim());
            }
            var fullComment = string.Join(" | ", commentParts);

            var approval = new WelfareApproval
            {
                RequestId = RequestId,
                ApproverId = employee?.Id ?? 0,
                ApproverLevel = "HRManager",
                Action = Action == "ConfirmPayment" ? "Paid" : "Rejected",
                Comments = fullComment,
                ActionDate = DateTime.Now
            };

            if (Action == "ConfirmPayment")
            {
                request.ApprovedAmount = finalAmount;
                request.CurrentLevel = "Completed";
                request.CurrentStatus = "PaymentCompleted";
                request.Status = "Paid";

                _db.WelfareApprovals.Add(approval);
                await _db.SaveChangesAsync();

                // Notify Employee of successful disbursement
                if (request.Employee != null && !string.IsNullOrEmpty(request.Employee.Email))
                {
                    try
                    {
                        await _notifService.CreateNotificationAsync(
                            request.Employee.Email,
                            "Welfare Payment Processed",
                            $"Your welfare payment of LKR {finalAmount:N2} for request WF-{request.RequestId:D4} ({request.WelfareType?.TypeName}) has been processed via {methodStr} (Ref: {refStr}).",
                            CoreNotificationType.Approved,
                            "/Welfare/StatusTracking?id=" + request.RequestId
                        );
                    }
                    catch { }
                }

                TempData["Success"] = $"Payment of LKR {finalAmount:N2} for WF-{request.RequestId:D4} ({request.Employee?.FullName}) has been confirmed and marked as Paid.";
            }
            else if (Action == "Rejected")
            {
                request.CurrentLevel = "Completed";
                request.CurrentStatus = "Rejected";
                request.Status = "Rejected";

                _db.WelfareApprovals.Add(approval);
                await _db.SaveChangesAsync();

                // Notify Employee of rejection
                if (request.Employee != null && !string.IsNullOrEmpty(request.Employee.Email))
                {
                    try
                    {
                        await _notifService.CreateNotificationAsync(
                            request.Employee.Email,
                            "Welfare Payment Rejected",
                            $"Your welfare request (WF-{request.RequestId:D4}) has been declined during final HR payment verification. Reason: {Comments ?? "Verification requirements not met."}",
                            CoreNotificationType.Rejected,
                            "/Welfare/StatusTracking?id=" + request.RequestId
                        );
                    }
                    catch { }
                }

                TempData["Success"] = $"Request WF-{request.RequestId:D4} has been rejected during HR payment verification.";
            }

            return RedirectToPage();
        }
    }
}
