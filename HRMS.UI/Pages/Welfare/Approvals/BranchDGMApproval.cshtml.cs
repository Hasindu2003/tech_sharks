using HRMS.Domain.Entities.Welfare;
using HRMS.Infrastructure.Identity;
using HRMS.Infrastructure.Persistence;
using HRMS.UI.Pages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HRMS.UI.Pages.Welfare.Approvals
{
    [Authorize(Roles = "BranchDGM")]
    public class BranchDGMApprovalModel : BasePageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;

        public BranchDGMApprovalModel(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
            : base(context)
        {
            _userManager = userManager;
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
                .Include(r => r.Documents)   // ← Load attached documents
                .Where(r => r.CurrentLevel == "BranchDGM" && r.CurrentStatus == "Pending")
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            var user = await _userManager.GetUserAsync(User);
            if (user != null)
            {
                var employee = await _db.Employees
                    .FirstOrDefaultAsync(e => e.Email == user.Email);
                if (employee != null)
                {
                    MyApprovedCount = await _db.WelfareApprovals
                        .CountAsync(a => a.ApproverLevel == "BranchDGM"
                                      && a.ApproverId == employee.Id
                                      && a.Action == "Approved");
                    MyRejectedCount = await _db.WelfareApprovals
                        .CountAsync(a => a.ApproverLevel == "BranchDGM"
                                      && a.ApproverId == employee.Id
                                      && a.Action == "Rejected");
                }
            }
        }

        public async Task<IActionResult> OnPostAsync()
        {
            await LoadCurrentUserAsync();

            var request = await _db.WelfareRequests.FindAsync(RequestId);
            if (request == null) return NotFound();

            var user = await _userManager.GetUserAsync(User);
            var employee = await _db.Employees
                .FirstOrDefaultAsync(e => e.Email == user!.Email);

            var approval = new WelfareApproval
            {
                RequestId = RequestId,
                ApproverId = employee?.Id ?? 0,
                ApproverLevel = "BranchDGM",
                Action = Action,
                Comments = Comments,
                ActionDate = DateTime.Now
            };

            if (Action == "Approved")
            {
                request.CurrentLevel = "HODGM";
                request.CurrentStatus = "Pending";
                request.Status = "UnderReview";
            }
            else if (Action == "Rejected")
            {
                request.CurrentStatus = "Rejected";
                request.Status = "Rejected";
            }

            _db.WelfareApprovals.Add(approval);
            await _db.SaveChangesAsync();

            TempData["Message"] = Action == "Approved"
                ? "Request approved and forwarded to HO DGM."
                : "Request has been rejected.";

            return RedirectToPage();
        }
    }
}
