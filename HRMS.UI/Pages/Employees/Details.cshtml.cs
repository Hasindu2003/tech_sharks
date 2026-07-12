using HRMS.Domain.Entities.Core;
using HRMS.Infrastructure.Identity;
using HRMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HRMS.UI.Pages.Employees
{
    using Employee = HRMS.Domain.Entities.Core.Employee;

    [Authorize(Roles = "HR Manager,Area Manager,Branch Manager")]

    public class DetailsModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public DetailsModel(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public Employee Employee { get; set; } = null!;
        public IList<EmployeeDocument> Documents { get; set; } = new List<EmployeeDocument>();

        public async Task<IActionResult> OnGetAsync(int id)
        {
            var emp = await _context.Employees
                .Include(e => e.Department)
                .Include(e => e.Designation)
                .Include(e => e.Branch)
                .Include(e => e.ReportingOfficer)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (emp == null)
                return NotFound();

            if (User.IsInRole("Area Manager"))
            {
                var currentUser = await _userManager.GetUserAsync(User);
                var allowed = ParseManagedBranches(currentUser?.ManagedBranches);
                if (allowed != null && !allowed.Contains(emp.BranchId))
                    return Forbid();
            }
            else if (User.IsInRole("Branch Manager"))
            {
                var currentUser = await _userManager.GetUserAsync(User);
                if (!string.IsNullOrWhiteSpace(currentUser?.Branch) &&
                    !string.Equals(emp.Branch?.Name, currentUser.Branch, StringComparison.OrdinalIgnoreCase))
                    return Forbid();
            }

            Employee = emp;
            Documents = await _context.EmployeeDocuments
                .Where(d => d.EmployeeId == id)
                .OrderByDescending(d => d.UploadedAt)
                .ToListAsync();
            return Page();
        }

        private static List<int>? ParseManagedBranches(string? csv)
        {
            if (string.IsNullOrWhiteSpace(csv)) return null;
            return csv.Split(',', StringSplitOptions.RemoveEmptyEntries)
                      .Select(s => int.TryParse(s.Trim(), out var id) ? id : 0)
                      .Where(id => id > 0)
                      .ToList();
        }
    }
}
