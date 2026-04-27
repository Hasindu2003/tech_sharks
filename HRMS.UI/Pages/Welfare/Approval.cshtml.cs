using HRMS.Domain.Entities.Welfare;
using HRMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HRMS.UI.Pages.Welfare
{
    public class ApprovalListModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public ApprovalListModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public List<WelfareRequest> Requests { get; set; } = new();
        public string ApproverLevel { get; set; } = string.Empty;

        // Stats
        public int MyApprovedCount { get; set; }
        public int MyRejectedCount { get; set; }

        // Temporary approver ID (replace with logged-in user later)
        private const int TempApproverId = 1;

        public async Task<IActionResult> OnGetAsync(string level)
        {
            var validLevels = new[] { "BranchDGM", "HODGM", "SeniorManagement" };
            if (string.IsNullOrWhiteSpace(level) || !validLevels.Contains(level))
                return RedirectToPage("/Index");

            ApproverLevel = level;

            Requests = await _context.WelfareRequests
                .Include(r => r.WelfareType)
                .Include(r => r.Employee)
                .Include(r => r.Documents)   // ← so we can show the attachment count badge
                .Where(r =>
                    r.CurrentLevel == level ||
                    _context.WelfareApprovals.Any(a =>
                        a.RequestId == r.RequestId &&
                        a.ApproverLevel == level)
                )
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            MyApprovedCount = await _context.WelfareApprovals
                .CountAsync(a =>
                    a.ApproverLevel == level &&
                    a.ApproverId == TempApproverId &&
                    a.Action == "Approved");

            MyRejectedCount = await _context.WelfareApprovals
                .CountAsync(a =>
                    a.ApproverLevel == level &&
                    a.ApproverId == TempApproverId &&
                    a.Action == "Rejected");

            return Page();
        }
    }
}
