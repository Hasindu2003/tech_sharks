using HRMS.Domain.Entities.Leave;
using HRMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HRMS.UI.Pages.Settings.LeavePolicies
{
    [Authorize(Roles = "Admin,HR Manager")]
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public IndexModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public IList<LeavePolicy> Policies { get; set; } = default!;

        public async Task OnGetAsync()
        {
            Policies = await _context.LeavePolicies.OrderBy(p => p.LeaveType).ToListAsync();
        }
    }
}
