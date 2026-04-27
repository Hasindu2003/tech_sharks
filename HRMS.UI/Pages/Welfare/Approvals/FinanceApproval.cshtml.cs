using HRMS.Domain.Entities.Welfare;
using HRMS.Infrastructure.Identity;
using HRMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HRMS.UI.Pages.Welfare.Approvals
{
    [Authorize(Roles = "Finance")]
    public class FinanceApprovalModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public FinanceApprovalModel(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
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

        public async Task OnGetAsync()
        {
            PendingRequests = await _context.WelfareRequests
                .Include(r => r.WelfareType)
                .Include(r => r.Employee)
                .Include(r => r.Documents)   // ← Load attached documents
                .Where(r => r.CurrentLevel == "Finance" && r.CurrentStatus == "Pending")
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            var user = await _userManager.GetUserAsync(User);
            if (user != null)
            {
                var employee = await _context.Employees
                    .FirstOrDefaultAsync(e => e.Email == user.Email);
                if (employee != null)
                {
                    MyApprovedCount = await _context.WelfareApprovals
                        .CountAsync(a => a.ApproverLevel == "Finance"
                                      && a.ApproverId == employee.Id
                                      && a.Action == "Approved");
                    MyRejectedCount = await _context.WelfareApprovals
                        .CountAsync(a => a.ApproverLevel == "Finance"
                                      && a.ApproverId == employee.Id
                                      && a.Action == "Rejected");
                }
            }
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var request = await _context.WelfareRequests.FindAsync(RequestId);
            if (request == null) return NotFound();

            var user = await _userManager.GetUserAsync(User);
            var employee = await _context.Employees
                .FirstOrDefaultAsync(e => e.Email == user!.Email);

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
                ApproverLevel = "Finance",
                Action = Action == "ConfirmPayment" ? "Approved" : "Rejected",
                Comments = fullComment,
                ActionDate = DateTime.Now
            };

            if (Action == "ConfirmPayment")
            {
                request.ApprovedAmount = ApprovedAmount ?? request.RequestedAmount;
                request.CurrentStatus = "PaymentCompleted";
                request.Status = "PaymentCompleted";
            }
            else if (Action == "Rejected")
            {
                request.CurrentStatus = "Rejected";
                request.Status = "Rejected";
            }

            _context.WelfareApprovals.Add(approval);
            await _context.SaveChangesAsync();

            TempData["Message"] = Action == "ConfirmPayment"
                ? $"Payment of LKR {(ApprovedAmount ?? request.RequestedAmount):N2} confirmed successfully."
                : "Request has been rejected at Finance stage.";

            return RedirectToPage();
        }
    }
}
