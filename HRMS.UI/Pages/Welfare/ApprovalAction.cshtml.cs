using HRMS.Domain.Entities.Welfare;
using HRMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HRMS.UI.Pages.Welfare
{
    public class ApprovalActionModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public ApprovalActionModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public new WelfareRequest? Request { get; set; }

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null) return NotFound();

            Request = await _context.WelfareRequests
                .Include(r => r.WelfareType)
                .Include(r => r.Employee)
                .Include(r => r.Documents)   // ← Load attached documents
                .FirstOrDefaultAsync(r => r.RequestId == id);

            if (Request == null) return NotFound();

            return Page();
        }
    }
}
