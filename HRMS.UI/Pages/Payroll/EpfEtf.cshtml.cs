using HRMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HRMS.UI.Pages.Payroll
{
    [Authorize(Roles = "Admin,Finance,SeniorManagement")]
    public class EpfEtfModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        public EpfEtfModel(ApplicationDbContext context) => _context = context;

        public List<EpfEtfRow> SalaryData { get; set; } = new();
        public decimal TotalEpfEmployee { get; set; }
        public decimal TotalEpfEmployer { get; set; }
        public decimal TotalEtf { get; set; }

        public async Task OnGetAsync()
        {
            // Get latest salary per employee
            var salaries = await _context.PayrollSalaries
                .Include(s => s.Employee)
                .GroupBy(s => s.EmployeeId)
                .Select(g => g.OrderByDescending(s => s.EffectiveDate).First())
                .ToListAsync();

            SalaryData = salaries.Select(s => new EpfEtfRow
            {
                Employee = s.Employee,
                BasicSalary = s.BasicSalary,
                EpfEmployee = Math.Round(s.BasicSalary * 0.08m, 2),
                EpfEmployer = Math.Round(s.BasicSalary * 0.12m, 2),
                Etf = Math.Round(s.BasicSalary * 0.03m, 2)
            }).OrderBy(r => r.Employee?.FirstName).ToList();

            TotalEpfEmployee = SalaryData.Sum(r => r.EpfEmployee);
            TotalEpfEmployer = SalaryData.Sum(r => r.EpfEmployer);
            TotalEtf = SalaryData.Sum(r => r.Etf);
        }
    }

    public class EpfEtfRow
    {
        public HRMS.Domain.Entities.Core.Employee? Employee { get; set; }
        public decimal BasicSalary { get; set; }
        public decimal EpfEmployee { get; set; }
        public decimal EpfEmployer { get; set; }
        public decimal Etf { get; set; }
    }
}
