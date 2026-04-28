using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using HRMS.Domain.Entities.Core;
using HRMS.Infrastructure.Identity;
using HRMS.Infrastructure.Persistence;

namespace HRMS.UI.Pages.Settings.Departments
{
    [Authorize(Roles = "Admin,HR Manager")]
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public IndexModel(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public IList<Department> Departments { get; set; } = default!;

        public async Task OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            var isAdmin = await _userManager.IsInRoleAsync(user, "Admin");

            var query = _context.Departments.Include(d => d.Branch).AsQueryable();

            if (!isAdmin)
            {
                // HR Manager logic: get their assigned branch
                if (user?.EmployeeId != null)
                {
                    var emp = await _context.Employees.FindAsync(user.EmployeeId);
                    if (emp != null)
                    {
                        query = query.Where(d => d.BranchId == emp.BranchId);
                    }
                    else
                    {
                        // Invalid employee ID, show nothing
                        query = query.Where(d => false);
                    }
                }
                else
                {
                    // No employee linked, show nothing
                    query = query.Where(d => false);
                }
            }

            Departments = await query.ToListAsync();
        }
    }
}
