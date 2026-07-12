using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HRMS.Domain.Entities.Attendance;
using HRMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HRMS.UI.Pages.BiometricLogs
{
    [Authorize(Roles = "Branch Manager, Area Manager, HR Manager")]
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        
        public List<BiometricLog> BiometricLogs { get; set; } = new();
        public List<Domain.Entities.Core.Employee> Employees { get; set; } = new();
        
        [BindProperty(SupportsGet = true)]
        public int? EmployeeId { get; set; }
        
        public IndexModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task OnGetAsync()
        {
            Employees = await _context.Employees.OrderBy(e => e.FullName).ToListAsync();
            
            var query = _context.BiometricLogs.Include(b => b.Employee).AsQueryable();
            
            if (EmployeeId.HasValue && EmployeeId.Value > 0)
            {
                query = query.Where(b => b.EmployeeId == EmployeeId.Value);
            }
            
            var limitDate = System.DateTime.Today.AddDays(-7);
            query = query.Where(b => b.LogDateTime >= limitDate);
            
            BiometricLogs = await query.OrderByDescending(b => b.LogDateTime).ToListAsync();
        }
    }
}
