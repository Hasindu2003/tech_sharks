using HRMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;

namespace HRMS.UI.Pages.Api
{
    [Authorize]
    [IgnoreAntiforgeryToken]
    public class WelfareDataModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public WelfareDataModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> OnGetEmployeeAsync(int id)
        {
            var emp = await _context.Employees
                .Include(e => e.Designation)
                .Include(e => e.Department)
                .Include(e => e.Branch)
                .FirstOrDefaultAsync(e => e.Id == id && e.NIC != "DUTY-ACC");

            if (emp == null)
            {
                return new JsonResult(new { message = "Employee not found." }) { StatusCode = 404 };
            }

            return new JsonResult(new
            {
                id = emp.Id,
                fullName = emp.FullName,
                nic = emp.NIC,
                email = emp.Email,
                phoneNumber = emp.PhoneNumber,
                epfNumber = emp.EPFNumber,
                designation = emp.Designation?.Title ?? "—",
                department = emp.Department?.Name ?? "—",
                branch = emp.Branch?.Name ?? "—",
                bankAccountName = emp.BankAccountName,
                bankAccountNumber = emp.BankAccountNumber,
                status = emp.Status
            });
        }
    }
}
