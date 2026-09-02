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
    [Authorize(Roles = "Area Manager")]
    public class AreaManagerApprovalModel : BasePageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly INotificationService _notifService;

        public AreaManagerApprovalModel(ApplicationDbContext context, UserManager<ApplicationUser> userManager, INotificationService notifService)
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

        public async Task OnGetAsync()
        {
            await LoadCurrentUserAsync();

            PendingRequests = await _db.WelfareRequests
                .Include(r => r.WelfareType)
                .Include(r => r.Employee)
                .Include(r => r.Documents)
                .Where(r => r.CurrentLevel == "AreaManager" && r.CurrentStatus == "Pending" && r.Employee != null && !r.Employee.NIC.StartsWith("DUTY") && r.Employee.NIC != "DUTY-ACC")
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            var user = await _userManager.GetUserAsync(User);
            if (user != null)
            {
                var employee = await _db.Employees
                    .FirstOrDefaultAsync(e => e.Email == user.Email && !e.NIC.StartsWith("DUTY") && e.NIC != "DUTY-ACC");
                if (employee != null)
                {
                    MyApprovedCount = await _db.WelfareApprovals
                        .CountAsync(a => a.ApproverLevel == "AreaManager"
                                      && a.ApproverId == employee.Id
                                      && a.Action == "Approved");
                    MyRejectedCount = await _db.WelfareApprovals
                        .CountAsync(a => a.ApproverLevel == "AreaManager"
                                      && a.ApproverId == employee.Id
                                      && a.Action == "Rejected");
                }
            }
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

            var approval = new WelfareApproval
            {
                RequestId = RequestId,
                ApproverId = employee?.Id ?? 0,
                ApproverLevel = "AreaManager",
                Action = Action,
                Comments = Comments,
                ActionDate = DateTime.Now
            };

            if (Action == "Approved")
            {
                request.CurrentLevel = "HRManager";
                request.CurrentStatus = "Pending";
                request.Status = "Approved";

                _db.WelfareApprovals.Add(approval);
                await _db.SaveChangesAsync();

                // Send notification to all HR Managers
                var hrManagers = await _userManager.GetUsersInRoleAsync("HR Manager");
                foreach (var hr in hrManagers)
                {
                    await _notifService.CreateNotificationAsync(
                        hr.Email!,
                        "Welfare Disbursement Pending",
                        $"A welfare request (WF-{request.RequestId:D4}) from {request.Employee?.FullName} is pending disbursement.",
                        CoreNotificationType.Info,
                        "/Welfare/Approvals/HRManagerApproval"
                    );
                }
            }
            else if (Action == "Rejected")
            {
                request.CurrentStatus = "Rejected";
                request.Status = "Rejected";

                _db.WelfareApprovals.Add(approval);
                await _db.SaveChangesAsync();

                // Notify request owner
                if (request.Employee != null)
                {
                    await _notifService.CreateNotificationAsync(
                        request.Employee.Email,
                        "Welfare Request Rejected",
                        $"Your welfare request (WF-{request.RequestId:D4}) has been rejected by the Area Manager.",
                        CoreNotificationType.Rejected,
                        "/Welfare/StatusTracking?id=" + request.RequestId
                    );
                }
            }

            TempData["Message"] = Action == "Approved"
                ? "Request approved and forwarded to HR Manager."
                : "Request has been rejected.";

            return RedirectToPage();
        }
    }
}
