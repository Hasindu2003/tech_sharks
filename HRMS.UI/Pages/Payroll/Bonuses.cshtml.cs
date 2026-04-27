using HRMS.Domain.Entities.Core;
using HRMS.Domain.Entities.Payroll;
using HRMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HRMS.UI.Pages.Payroll
{
    [Authorize(Roles = "Admin,Finance,SeniorManagement")]
    public class BonusesModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public BonusesModel(ApplicationDbContext context) => _context = context;

        public List<PayrollBonus> Bonuses { get; set; } = new();
        public List<Employee> Employees { get; set; } = new();
        public decimal TotalThisMonth { get; set; }

        [BindProperty] public int EmployeeId { get; set; }
        [BindProperty] public string BonusType { get; set; } = string.Empty;
        [BindProperty] public decimal Amount { get; set; }
        [BindProperty] public string PayrollMonth { get; set; } = string.Empty;
        [BindProperty] public string? Reason { get; set; }

        public async Task OnGetAsync()
        {
            await LoadDataAsync();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            try
            {
                var parts = PayrollMonth.Split('-');
                var year = int.Parse(parts[0]);
                var month = int.Parse(parts[1]);

                _context.PayrollBonuses.Add(new PayrollBonus
                {
                    EmployeeId = EmployeeId,
                    BonusType = BonusType,
                    Amount = Amount,
                    Month = month,
                    Year = year,
                    Reason = Reason
                });

                await _context.SaveChangesAsync();
                TempData["Success"] = "Bonus assigned successfully!";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error: " + ex.Message;
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDeleteAsync(int bonusId)
        {
            var bonus = await _context.PayrollBonuses.FindAsync(bonusId);
            if (bonus != null)
            {
                _context.PayrollBonuses.Remove(bonus);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Bonus deleted successfully!";
            }
            return RedirectToPage();
        }

        private async Task LoadDataAsync()
        {
            var now = DateTime.Now;

            Employees = await _context.Employees
                .Include(e => e.Department)
                .Include(e => e.Designation)
                .Where(e => e.Status == "Active")
                .OrderBy(e => e.FirstName)
                .ToListAsync();

            Bonuses = await _context.PayrollBonuses
                .Include(b => b.Employee)
                    .ThenInclude(e => e!.Designation)
                .OrderByDescending(b => b.Year)
                .ThenByDescending(b => b.Month)
                .ToListAsync();

            TotalThisMonth = await _context.PayrollBonuses
                .Where(b => b.Month == now.Month && b.Year == now.Year)
                .SumAsync(b => (decimal?)b.Amount) ?? 0;
        }
    }
}
