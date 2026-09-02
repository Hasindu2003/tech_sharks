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
    [Authorize(Roles = "HR Manager,HR Officer,Admin")]
    public class HRManagerApprovalModel : BasePageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly INotificationService _notifService;

        public HRManagerApprovalModel(ApplicationDbContext context, UserManager<ApplicationUser> userManager, INotificationService notifService)
            : base(context)
        {
            _userManager = userManager;
            _notifService = notifService;
        }

        public List<WelfareRequest> PendingRequests { get; set; } = new();
        public int MyApprovedCount { get; set; }
        public int MyRejectedCount { get; set; }

        [BindProperty] public int RequestId { get; set; }
        [BindProperty] public string Action { get; set; } = string.Empty;
        [BindProperty] public string? Comments { get; set; }
        [BindProperty] public string? PaymentReference { get; set; }
        [BindProperty] public decimal? ApprovedAmount { get; set; }
        [BindProperty] public string? PaymentDate { get; set; }

        public IActionResult OnGet()
        {
            return RedirectToPage("/Welfare/Payments");
        }

        public async Task<IActionResult> OnPostAsync()
        {
            await LoadCurrentUserAsync();

            var request = await _db.WelfareRequests
                .Include(r => r.Employee)
                .FirstOrDefaultAsync(r => r.RequestId == RequestId && r.Employee != null && !r.Employee.NIC.StartsWith("DUTY") && r.Employee.NIC != "DUTY-ACC");

            if (request == null) return NotFound();

            var user = await _userManager.GetUserAsync(User);
            var employee = await _db.Employees
                .FirstOrDefaultAsync(e => e.Email == user!.Email && !e.NIC.StartsWith("DUTY") && e.NIC != "DUTY-ACC");

            // Build full comment
            var commentParts = new List<string>();
            if (!string.IsNullOrEmpty(PaymentReference))
                commentParts.Add($"Payment Ref: {PaymentReference}");
            if (!string.IsNullOrEmpty(PaymentDate))
                commentParts.Add($"Payment Date: {PaymentDate}");
            if (!string.IsNullOrEmpty(Comments))
                commentParts.Add(Comments);
            var fullComment = string.Join(". ", commentParts);

            var approval = new WelfareApproval
            {
                RequestId = RequestId,
                ApproverId = employee?.Id ?? 0,
                ApproverLevel = "HRManager",
                Action = Action == "ConfirmPayment" ? "Approved" : "Rejected",
                Comments = fullComment,
                ActionDate = DateTime.Now
            };

            if (Action == "ConfirmPayment")
            {
                request.ApprovedAmount = ApprovedAmount ?? request.RequestedAmount;
                request.CurrentStatus = "PaymentCompleted";
                request.Status = "PaymentCompleted";

                _db.WelfareApprovals.Add(approval);
                await _db.SaveChangesAsync();

                // Notify request owner of disbursement
                if (request.Employee != null)
                {
                    await _notifService.CreateNotificationAsync(
                        request.Employee.Email,
                        "Welfare Request Disbursed",
                        $"Your welfare request (WF-{request.RequestId:D4}) of LKR {request.ApprovedAmount.Value:N2} has been disbursed.",
                        CoreNotificationType.Approved,
                        "/Welfare/StatusTracking?id=" + request.RequestId
                    );
                }
            }
            else if (Action == "Rejected")
            {
                request.CurrentStatus = "Rejected";
                request.Status = "Rejected";

                _db.WelfareApprovals.Add(approval);
                await _db.SaveChangesAsync();

                // Notify request owner of rejection
                if (request.Employee != null)
                {
                    await _notifService.CreateNotificationAsync(
                        request.Employee.Email,
                        "Welfare Request Rejected",
                        $"Your welfare request (WF-{request.RequestId:D4}) has been rejected during final disbursement review.",
                        CoreNotificationType.Rejected,
                        "/Welfare/StatusTracking?id=" + request.RequestId
                    );
                }
            }

            TempData["Message"] = Action == "ConfirmPayment"
                ? $"Payment of LKR {(ApprovedAmount ?? request.RequestedAmount):N2} confirmed successfully."
                : "Request has been rejected at final disbursement review.";

            return RedirectToPage();
        }
    }
}
