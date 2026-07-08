using HRMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HRMS.UI.Pages.Api
{
    [Authorize]
    [IgnoreAntiforgeryToken]
    public class EmployeeDataModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public EmployeeDataModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> OnGetSearchAsync([FromQuery] string term)
        {
            if (string.IsNullOrWhiteSpace(term))
                return new JsonResult(new List<object>());

            var termLower = term.ToLower();

            // Search by EPF or Name in AspNetUsers (ApplicationUser)
            var users = await _context.Users
                .Where(u => u.EpfNumber.Contains(term) || 
                            u.FullName.ToLower().Contains(termLower))
                .Take(10)
                .Select(u => new
                {
                    fullName = u.FullName,
                    epfNumber = u.EpfNumber,
                    email = u.Email,
                    branch = u.Branch,
                    department = u.Department ?? "N/A",
                    designation = u.Designation
                })
                .ToListAsync();

            return new JsonResult(users);
        }

        public async Task<IActionResult> OnGetByEpfAsync([FromQuery] string epf)
        {
            if (string.IsNullOrWhiteSpace(epf)) return new BadRequestObjectResult("EPF is required");

            var u = await _context.Users
                .FirstOrDefaultAsync(u => u.EpfNumber == epf);

            if (u == null) return new NotFoundObjectResult("Employee not found");

            return new JsonResult(new
            {
                fullName = u.FullName,
                epfNumber = u.EpfNumber,
                email = u.Email,
                branch = u.Branch,
                department = u.Department ?? "N/A",
                designation = u.Designation
            });
        }

        // Test handler to verify the API is reachable
        public IActionResult OnGetTest()
        {
            return new JsonResult(new { status = "API is working", time = DateTime.Now });
        }
    }
}
