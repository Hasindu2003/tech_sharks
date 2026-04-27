using HRMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HRMS.UI.Pages.Welfare
{
    [Authorize(Roles = "Employee")]
    public class EditRequestModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public EditRequestModel(ApplicationDbContext context) => _context = context;

        // ✅ Renamed from Request to WelfareRequest to avoid conflict with PageModel.Request
        public HRMS.Domain.Entities.Welfare.WelfareRequest? WelfareRequest { get; set; }

        [BindProperty] public int WelfareTypeId { get; set; }
        [BindProperty] public string RequestDate { get; set; } = string.Empty;
        [BindProperty] public decimal RequestedAmount { get; set; }
        [BindProperty] public string Remark { get; set; } = string.Empty;

        public async Task<IActionResult> OnGetAsync(int id)
        {
            WelfareRequest = await _context.WelfareRequests
                .Include(r => r.WelfareType)
                .Include(r => r.Employee)
                .FirstOrDefaultAsync(r => r.RequestId == id);

            if (WelfareRequest == null) return NotFound();

            // ── Security: must belong to this employee ────────────────────────
            var userEmail = User.Identity?.Name;
            var employee = await _context.Employees
                .FirstOrDefaultAsync(e => e.Email == userEmail);

            if (employee == null || WelfareRequest.EmployeeId != employee.Id)
                return Forbid();

            // ── Check edit conditions ─────────────────────────────────────────
            if (!CanEdit(WelfareRequest))
            {
                TempData["Error"] = GetEditBlockReason(WelfareRequest);
                return RedirectToPage("/Welfare/RequestList");
            }

            // ── Pre-fill bound properties ─────────────────────────────────────
            WelfareTypeId = WelfareRequest.WelfareTypeId;
            RequestDate = WelfareRequest.RequestDate.ToString("yyyy-MM-dd");
            RequestedAmount = WelfareRequest.RequestedAmount;
            Remark = WelfareRequest.Remark ?? "";

            return Page();
        }

        public async Task<IActionResult> OnPostAsync(int id)
        {
            var request = await _context.WelfareRequests
                .FirstOrDefaultAsync(r => r.RequestId == id);

            if (request == null) return NotFound();

            // ── Security: must belong to this employee ────────────────────────
            var userEmail = User.Identity?.Name;
            var employee = await _context.Employees
                .FirstOrDefaultAsync(e => e.Email == userEmail);

            if (employee == null || request.EmployeeId != employee.Id)
                return Forbid();

            // ── Re-validate edit window on POST ───────────────────────────────
            if (!CanEdit(request))
            {
                TempData["Error"] = GetEditBlockReason(request);
                return RedirectToPage("/Welfare/RequestList");
            }

            // ── Update allowed fields ─────────────────────────────────────────
            request.WelfareTypeId = WelfareTypeId;
            request.RequestDate = DateTime.Parse(RequestDate);
            request.RequestedAmount = RequestedAmount;
            request.Remark = Remark;

            await _context.SaveChangesAsync();

            TempData["Success"] = "Request updated successfully!";
            return RedirectToPage("/Welfare/RequestList");
        }

        private static bool CanEdit(HRMS.Domain.Entities.Welfare.WelfareRequest req)
        {
            if (req.CurrentLevel != "BranchDGM") return false;
            if (req.CurrentStatus != "Pending") return false;
            if (DateTime.Now > req.CreatedAt.AddHours(24)) return false;
            return true;
        }

        private static string GetEditBlockReason(HRMS.Domain.Entities.Welfare.WelfareRequest req)
        {
            if (DateTime.Now > req.CreatedAt.AddHours(24))
                return "The 24-hour edit window has expired. This request can no longer be edited.";
            if (req.CurrentLevel != "BranchDGM" || req.CurrentStatus != "Pending")
                return "This request is already under review and cannot be edited.";
            return "This request cannot be edited.";
        }
    }
}
