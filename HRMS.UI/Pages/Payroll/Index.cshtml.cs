using HRMS.Domain.Entities.Payroll;
using HRMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HRMS.UI.Pages.Payroll
{
    [Authorize(Roles = "Admin,Finance,SeniorManagement")]
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public IndexModel(ApplicationDbContext context) => _context = context;

        public string CurrentMonthName { get; set; } = string.Empty;
        public decimal TotalPayroll { get; set; }
        public decimal LastMonthPayroll { get; set; }
        public string LastMonthName { get; set; } = string.Empty;   // ✅ Issue 3
        public int TotalEmployees { get; set; }
        public decimal TotalBonuses { get; set; }
        public decimal TotalDeductions { get; set; }
        public List<PayrollRun> PastRuns { get; set; } = new();

        // ✅ Issue 2 — Dynamic checklist flags
        public bool HasSalaryRecords { get; set; }
        public bool HasBonusRecords { get; set; }
        public bool HasPayrollRun { get; set; }
        public bool HasPayslips { get; set; }
        public int AnomalyCount { get; set; } = 3; // sample — replace with real attendance data

        public async Task OnGetAsync()
        {
            var now = DateTime.Now;
            CurrentMonthName = now.ToString("MMMM yyyy");

            // ✅ Issue 3 — Always show last month name
            var lastMonthDate = now.AddMonths(-1);
            LastMonthName = lastMonthDate.ToString("MMM yyyy");

            // Total employees with salary records
            TotalEmployees = await _context.PayrollSalaries
                .Select(s => s.EmployeeId)
                .Distinct()
                .CountAsync();

            // Estimate total payroll from salary records
            var salaries = await _context.PayrollSalaries
                .GroupBy(s => s.EmployeeId)
                .Select(g => g.OrderByDescending(s => s.EffectiveDate).First())
                .ToListAsync();

            TotalPayroll = salaries.Sum(s =>
                s.BasicSalary + s.HousingAllowance +
                s.TransportAllowance + s.MedicalAllowance);

            // Total bonuses this month
            TotalBonuses = await _context.PayrollBonuses
                .Where(b => b.Month == now.Month && b.Year == now.Year)
                .SumAsync(b => (decimal?)b.Amount) ?? 0;

            // Total deductions = EPF(8%) + ETF(3%)
            TotalDeductions = salaries.Sum(s => s.BasicSalary * 0.11m);

            // ✅ Issue 3 — Last month total (show 0 if no run)
            var lastMonth = await _context.PayrollRuns
                .Where(r => r.Month == lastMonthDate.Month && r.Year == lastMonthDate.Year)
                .FirstOrDefaultAsync();
            LastMonthPayroll = lastMonth?.TotalAmount ?? 0;

            // Past 5 payroll runs
            PastRuns = await _context.PayrollRuns
                .Where(r => r.Status == "Completed")
                .OrderByDescending(r => r.Year)
                .ThenByDescending(r => r.Month)
                .Take(5)
                .ToListAsync();

            // ✅ Issue 2 — Dynamic checklist
            HasSalaryRecords = salaries.Any();
            HasBonusRecords = await _context.PayrollBonuses
                .AnyAsync(b => b.Month == now.Month && b.Year == now.Year);
            HasPayrollRun = await _context.PayrollRuns
                .AnyAsync(r => r.Month == now.Month && r.Year == now.Year);
            HasPayslips = await _context.Payslips
                .AnyAsync(p => p.PayrollRun!.Month == now.Month && p.PayrollRun.Year == now.Year);
        }

        public async Task<IActionResult> OnPostRunPayrollAsync()
        {
            var now = DateTime.Now;

            var existing = await _context.PayrollRuns
                .FirstOrDefaultAsync(r => r.Month == now.Month && r.Year == now.Year);

            if (existing != null)
            {
                TempData["Success"] = $"Payroll for {now:MMMM yyyy} has already been processed.";
                return RedirectToPage();
            }

            var salaries = await _context.PayrollSalaries
                .Include(s => s.Employee)
                .GroupBy(s => s.EmployeeId)
                .Select(g => g.OrderByDescending(s => s.EffectiveDate).First())
                .ToListAsync();

            if (!salaries.Any())
            {
                TempData["Success"] = "No salary records found. Please add salary records first.";
                return RedirectToPage();
            }

            var bonuses = await _context.PayrollBonuses
                .Where(b => b.Month == now.Month && b.Year == now.Year)
                .ToListAsync();

            var run = new PayrollRun
            {
                Month = now.Month,
                Year = now.Year,
                Status = "Completed",
                ProcessedAt = DateTime.Now,
                TotalEmployees = salaries.Count
            };

            _context.PayrollRuns.Add(run);
            await _context.SaveChangesAsync();

            decimal totalAmount = 0;

            foreach (var sal in salaries)
            {
                var empBonus = bonuses.Where(b => b.EmployeeId == sal.EmployeeId).Sum(b => b.Amount);
                var grossPay = sal.BasicSalary + sal.HousingAllowance +
                                  sal.TransportAllowance + sal.MedicalAllowance + empBonus;
                var epfEmployee = Math.Round(sal.BasicSalary * 0.08m, 2);
                var epfEmployer = Math.Round(sal.BasicSalary * 0.12m, 2);
                var etf = Math.Round(sal.BasicSalary * 0.03m, 2);
                var tax = 0m;
                var totalDed = epfEmployee + tax;
                var netPay = grossPay - totalDed;

                _context.Payslips.Add(new Payslip
                {
                    PayrollRunId = run.Id,
                    EmployeeId = sal.EmployeeId,
                    BasicSalary = sal.BasicSalary,
                    HousingAllowance = sal.HousingAllowance,
                    TransportAllowance = sal.TransportAllowance,
                    MedicalAllowance = sal.MedicalAllowance,
                    Bonuses = empBonus,
                    GrossPay = grossPay,
                    EpfEmployee = epfEmployee,
                    EpfEmployer = epfEmployer,
                    Etf = etf,
                    TaxDeduction = tax,
                    TotalDeductions = totalDed,
                    NetPay = netPay,
                    Status = "Completed"
                });

                totalAmount += netPay;
            }

            run.TotalAmount = totalAmount;
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Payroll for {now:MMMM yyyy} processed! {salaries.Count} payslips generated.";
            return RedirectToPage();
        }
    }
}
