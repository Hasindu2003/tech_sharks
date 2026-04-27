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
    [Authorize(Roles = "SeniorManagement")]
    public class SeniorManagementApprovalModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public SeniorManagementApprovalModel(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public List<WelfareRequest> PendingRequests { get; set; } = new();
        public int MyApprovedCount { get; set; }
        public int MyRejectedCount { get; set; }

        [BindProperty] public int RequestId { get; set; }
        [BindProperty] public string Action { get; set; } = string.Empty;
        [BindProperty] public decimal? ApprovedAmount { get; set; }
        [BindProperty] public string? Comments { get; set; }

        public async Task OnGetAsync()
        {
            PendingRequests = await _context.WelfareRequests
                .Include(r => r.WelfareType)
                .Include(r => r.Employee)
                .Include(r => r.Documents)   // ← Load attached documents
                .Where(r => r.CurrentLevel == "SeniorManagement" && r.CurrentStatus == "Pending")
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
                        .CountAsync(a => a.ApproverLevel == "SeniorManagement"
                                      && a.ApproverId == employee.Id
                                      && a.Action == "Approved");
                    MyRejectedCount = await _context.WelfareApprovals
                        .CountAsync(a => a.ApproverLevel == "SeniorManagement"
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

            var approval = new WelfareApproval
            {
                RequestId = RequestId,
                ApproverId = employee?.Id ?? 0,
                ApproverLevel = "SeniorManagement",
                Action = Action,
                Comments = Comments,
                ActionDate = DateTime.Now
            };

            if (Action == "Approved")
            {
                // Senior Management sets the final approved amount
                request.ApprovedAmount = ApprovedAmount ?? request.RequestedAmount;
                request.CurrentLevel = "Finance";
                request.CurrentStatus = "Pending";
                request.Status = "Approved";
            }
            else if (Action == "Rejected")
            {
                request.CurrentStatus = "Rejected";
                request.Status = "Rejected";
            }

            _context.WelfareApprovals.Add(approval);
            await _context.SaveChangesAsync();

            TempData["Message"] = Action == "Approved"
                ? "Request approved and forwarded to Finance Department."
                : "Request has been rejected.";

            return RedirectToPage();
        }
    }
}
