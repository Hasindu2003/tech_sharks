using HRMS.Domain.Entities.Welfare;
using HRMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HRMS.UI.Pages.Welfare
{
    public class StatusTrackingModel : BasePageModel
    {
        public StatusTrackingModel(ApplicationDbContext context)
            : base(context) { }

        public WelfareRequest? WelfareRequest { get; set; }
        public List<WelfareApproval> Approvals { get; set; } = new();
        public List<WelfareDocument> Documents { get; set; } = new();

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            await LoadCurrentUserAsync();

            if (id == null || id <= 0)
                return RedirectToPage("/Welfare/RequestList");

            WelfareRequest = await _db.WelfareRequests
                .Include(r => r.WelfareType)
                .Include(r => r.Employee)
                    .ThenInclude(e => e!.Designation)
                .Include(r => r.Employee)
                    .ThenInclude(e => e!.Department)
                .Include(r => r.Employee)
                    .ThenInclude(e => e!.Branch)
                .FirstOrDefaultAsync(r => r.RequestId == id && r.Employee != null && !r.Employee.NIC.StartsWith("DUTY") && r.Employee.NIC != "DUTY-ACC");

            if (WelfareRequest == null)
                return RedirectToPage("/Welfare/RequestList");

            Approvals = await _db.WelfareApprovals
                .Where(a => a.RequestId == id)
                .OrderBy(a => a.ActionDate)
                .ToListAsync();

            Documents = await _db.WelfareDocuments
                .Where(d => d.RequestId == id)
                .ToListAsync();

            return Page();
        }
    }
}
