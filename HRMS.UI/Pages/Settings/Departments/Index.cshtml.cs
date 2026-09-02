using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using HRMS.Domain.Entities.Core;
using HRMS.Infrastructure.Persistence;

namespace HRMS.UI.Pages.Settings.Departments
{
    [Authorize(Roles = "Admin")]
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public IndexModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public IList<Department> Departments { get; set; } = default!;

        public async Task OnGetAsync()
        {
            Departments = await _context.Departments
                .Include(d => d.BranchDepartments)
                    .ThenInclude(bd => bd.Branch)
                .Where(d => d.Name != "Managerial" && d.Name != "Management")
                .OrderBy(d => d.Name)
                .ToListAsync();
        }
    }
}
