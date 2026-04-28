using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using HRMS.Domain.Entities.Core;
using HRMS.Infrastructure.Persistence;

namespace HRMS.UI.Pages.Employees
{
    [Authorize]
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public IndexModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public IList<Employee> EmployeeList { get; set; } = default!;

        public async Task OnGetAsync()
        {
            EmployeeList = await _context.Employees
                .Include(e => e.Department)
                .Include(e => e.Designation)
                .ToListAsync();
        }
    }
}
