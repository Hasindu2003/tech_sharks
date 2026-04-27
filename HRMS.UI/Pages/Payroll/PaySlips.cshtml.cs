using HRMS.Domain.Entities.Payroll;
using HRMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HRMS.UI.Pages.Payroll
{
    [Authorize(Roles = "Admin,Finance,SeniorManagement,Employee")]
    public class PayslipsModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        public PayslipsModel(ApplicationDbContext context) => _context = context;

        public List<Payslip> Payslips { get; set; } = new();
        public List<PayrollRun> PayrollRuns { get; set; } = new();
        public bool IsEmployee { get; set; }

        public async Task OnGetAsync()
        {
            IsEmployee = User.IsInRole("Employee");

            PayrollRuns = await _context.PayrollRuns
                .Where(r => r.Status == "Completed")
                .OrderByDescending(r => r.Year)
                .ThenByDescending(r => r.Month)
                .ToListAsync();

            if (IsEmployee)
            {
                // ── Employee: only see their own payslips ──────────────────────
                var userEmail = User.Identity?.Name;
                var employee = await _context.Employees
                    .FirstOrDefaultAsync(e => e.Email == userEmail);

                if (employee != null)
                {
                    Payslips = await _context.Payslips
                        .Include(p => p.Employee)
                            .ThenInclude(e => e!.Designation)
                        .Include(p => p.PayrollRun)
                        .Where(p => p.EmployeeId == employee.Id)
                        .OrderByDescending(p => p.PayrollRun!.Year)
                        .ThenByDescending(p => p.PayrollRun!.Month)
                        .ToListAsync();
                }
            }
            else
            {
                // ── Admin / Finance / SeniorManagement: see all payslips ───────
                Payslips = await _context.Payslips
                    .Include(p => p.Employee)
                        .ThenInclude(e => e!.Designation)
                    .Include(p => p.PayrollRun)
                    .OrderByDescending(p => p.PayrollRun!.Year)
                    .ThenByDescending(p => p.PayrollRun!.Month)
                    .ThenBy(p => p.Employee!.FirstName)
                    .ToListAsync();
            }
        }
    }
}
