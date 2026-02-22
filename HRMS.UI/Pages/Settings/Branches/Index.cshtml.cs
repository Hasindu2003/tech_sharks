using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using HRMS.Domain.Entities.Core;
using HRMS.Infrastructure.Persistence;

namespace HRMS.UI.Pages.Settings.Branches
{
    [Authorize(Roles = "Admin")]
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public IndexModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public IList<Branch> Branches { get; set; } = default!;

        public async Task OnGetAsync()
        {
            Branches = await _context.Branches.ToListAsync();
        }
    }
}
