using HRMS.Domain.Entities.Payroll;
using HRMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HRMS.UI.Pages.Payroll
{
    [Authorize(Roles = "Admin,Finance,SeniorManagement,Employee")]
    public class PayslipPdfModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public PayslipPdfModel(ApplicationDbContext context) => _context = context;

        public Payslip? Payslip { get; set; }

        // Extra bonus total fetched from PayrollBonuses for this employee + month
        public decimal BonusTotal { get; set; }
        public List<PayrollBonus> BonusDetails { get; set; } = new();

        // Recalculated totals that include bonuses
        public decimal AdjustedGrossPay { get; set; }
        public decimal AdjustedNetPay { get; set; }
        public decimal AdjustedTotalDeductions { get; set; }

        public async Task<IActionResult> OnGetAsync(int id)
        {
            Payslip = await _context.Payslips
                .Include(p => p.Employee)
                    .ThenInclude(e => e!.Designation)
                .Include(p => p.Employee)
                    .ThenInclude(e => e!.Department)
                .Include(p => p.PayrollRun)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (Payslip == null)
                return NotFound();

            // ── Employee can only view their own payslip ──────────────────────
            if (User.IsInRole("Employee"))
            {
                var userEmail = User.Identity?.Name;
                var employee = await _context.Employees
                    .FirstOrDefaultAsync(e => e.Email == userEmail);
                if (employee == null || Payslip.EmployeeId != employee.Id)
                    return Forbid();
            }

            // ── Load bonuses for this employee for the same month/year ────────
            if (Payslip.PayrollRun != null)
            {
                BonusDetails = await _context.PayrollBonuses
                    .Where(b => b.EmployeeId == Payslip.EmployeeId
                             && b.Month == Payslip.PayrollRun.Month
                             && b.Year == Payslip.PayrollRun.Year)
                    .ToListAsync();

                BonusTotal = BonusDetails.Sum(b => b.Amount);
            }

            // ── Recalculate gross, deductions, net including bonus ─────────────
            // Use the bonus already stored in Payslip.Bonuses PLUS any extra from
            // PayrollBonuses that were added after the payroll run.
            var totalBonuses = Payslip.Bonuses + BonusTotal;

            AdjustedGrossPay = Payslip.BasicSalary
                             + Payslip.HousingAllowance
                             + Payslip.TransportAllowance
                             + Payslip.MedicalAllowance
                             + totalBonuses;

            // Recalculate EPF on adjusted gross (8% employee)
            var epfEmployee = Math.Round(Payslip.BasicSalary * 0.08m, 2);
            var taxDeduction = Payslip.TaxDeduction;

            AdjustedTotalDeductions = epfEmployee + taxDeduction;
            AdjustedNetPay = AdjustedGrossPay - AdjustedTotalDeductions;

            // Persist the updated values back to the Payslip row so future
            // loads are consistent
            if (BonusTotal > 0)
            {
                Payslip.Bonuses = totalBonuses;
                Payslip.GrossPay = AdjustedGrossPay;
                Payslip.TotalDeductions = AdjustedTotalDeductions;
                Payslip.NetPay = AdjustedNetPay;
                Payslip.EpfEmployee = epfEmployee;

                await _context.SaveChangesAsync();

                // Clear BonusDetails so we don't double-count on re-renders
                BonusDetails.Clear();
                BonusTotal = 0;
            }
            else
            {
                // No extra bonuses — just use payslip values as-is
                AdjustedGrossPay = Payslip.GrossPay;
                AdjustedTotalDeductions = Payslip.TotalDeductions;
                AdjustedNetPay = Payslip.NetPay;
            }

            return Page();
        }
    }
}
