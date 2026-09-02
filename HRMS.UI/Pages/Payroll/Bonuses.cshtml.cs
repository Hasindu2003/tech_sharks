using HRMS.Domain.Entities.Core;
using HRMS.Domain.Entities.Payroll;
using HRMS.Infrastructure.Persistence;
using HRMS.UI.Pages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HRMS.UI.Pages.Payroll
{
    [Authorize(Roles = "HR Manager,HR Officer")]
    public class BonusesModel : BasePageModel
    {
        public BonusesModel(ApplicationDbContext db) : base(db) { }

        public List<PayrollBonus> Bonuses { get; set; } = new();
        public List<HRMS.Domain.Entities.Core.Employee> Employees { get; set; } = new();
        public List<string> DepartmentsList { get; set; } = new();

        public decimal TotalThisMonth { get; set; }
        public int ActiveBeneficiariesCount { get; set; }
        public string TopAllowanceCategory { get; set; } = "None";
        public decimal AverageAllowancePerBeneficiary { get; set; }

        public List<Branch> ManagedBranchesList { get; set; } = new();
        [BindProperty(SupportsGet = true)] public int? BranchId { get; set; }
        [BindProperty(SupportsGet = true)] public int? Month { get; set; }
        [BindProperty(SupportsGet = true)] public int? Year { get; set; }

        public Branch? CurrentBranch { get; set; }
        public string SelectedMonthName { get; set; } = string.Empty;

        // Form Fields (Create)
        [BindProperty] public int EmployeeId { get; set; }
        [BindProperty] public string BonusType { get; set; } = string.Empty;
        [BindProperty] public decimal Amount { get; set; }
        [BindProperty] public string PayrollMonth { get; set; } = string.Empty;
        [BindProperty] public string? Reason { get; set; }

        public async Task<IActionResult> OnGetAsync(int? branchId, int? month, int? year)
        {
            if (User.IsInRole("Admin"))
            {
                return Forbid();
            }

            var now = DateTime.Now;
            Month = month ?? Month ?? now.Month;
            Year = year ?? Year ?? now.Year;
            SelectedMonthName = new DateTime(Year.Value, Month.Value, 1).ToString("MMMM yyyy");

            await LoadCurrentUserAsync();
            await ResolveBranchesAsync(branchId);
            await LoadDataAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (User.IsInRole("Admin"))
            {
                return Forbid();
            }

            await LoadCurrentUserAsync();
            try
            {
                var parts = PayrollMonth.Split('-');
                var year = int.Parse(parts[0]);
                var month = int.Parse(parts[1]);

                _db.PayrollBonuses.Add(new PayrollBonus
                {
                    EmployeeId = EmployeeId,
                    BonusType = BonusType,
                    Amount = Amount,
                    Month = month,
                    Year = year,
                    Reason = Reason
                });

                await _db.SaveChangesAsync();
                TempData["Success"] = "Allowance assigned successfully!";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error assigning allowance: " + ex.Message;
            }

            return RedirectToPage(new { branchId = BranchId, month = Month, year = Year });
        }

        public async Task<IActionResult> OnPostEditAsync(int editBonusId, int editEmployeeId, string editBonusType, decimal editAmount, string editPayrollMonth, string? editReason)
        {
            if (User.IsInRole("Admin"))
            {
                return Forbid();
            }

            await LoadCurrentUserAsync();
            try
            {
                var bonus = await _db.PayrollBonuses.FindAsync(editBonusId);
                if (bonus == null)
                {
                    TempData["Error"] = "Allowance record not found.";
                    return RedirectToPage(new { branchId = BranchId, month = Month, year = Year });
                }

                var parts = editPayrollMonth.Split('-');
                var year = int.Parse(parts[0]);
                var month = int.Parse(parts[1]);

                bonus.EmployeeId = editEmployeeId;
                bonus.BonusType = editBonusType;
                bonus.Amount = editAmount;
                bonus.Month = month;
                bonus.Year = year;
                bonus.Reason = editReason;

                await _db.SaveChangesAsync();
                TempData["Success"] = "Allowance updated successfully!";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error updating allowance: " + ex.Message;
            }

            return RedirectToPage(new { branchId = BranchId, month = Month, year = Year });
        }

        public async Task<IActionResult> OnPostDeleteAsync(int bonusId)
        {
            if (User.IsInRole("Admin"))
            {
                return Forbid();
            }

            await LoadCurrentUserAsync();
            var bonus = await _db.PayrollBonuses.FindAsync(bonusId);
            if (bonus != null)
            {
                _db.PayrollBonuses.Remove(bonus);
                await _db.SaveChangesAsync();
                TempData["Success"] = "Allowance record deleted successfully!";
            }
            return RedirectToPage(new { branchId = BranchId, month = Month, year = Year });
        }

        private async Task ResolveBranchesAsync(int? branchId)
        {
            var username = User.Identity?.Name;
            var userAccount = await _db.Users.FirstOrDefaultAsync(u => u.UserName == username || u.Email == username);

            if (User.IsInRole("HR Officer"))
            {
                var allowedIds = ParseManagedBranches(userAccount?.ManagedBranches);
                if (allowedIds != null && allowedIds.Any())
                {
                    ManagedBranchesList = await _db.Branches
                        .Where(b => allowedIds.Contains(b.Id))
                        .OrderBy(b => b.Name)
                        .ToListAsync();
                }
                else
                {
                    ManagedBranchesList = await _db.Branches.OrderBy(b => b.Name).ToListAsync();
                }
            }
            else
            {
                ManagedBranchesList = await _db.Branches.OrderBy(b => b.Name).ToListAsync();
            }

            if (!ManagedBranchesList.Any())
            {
                ManagedBranchesList = await _db.Branches.OrderBy(b => b.Name).ToListAsync();
            }

            if (branchId.HasValue && ManagedBranchesList.Any(b => b.Id == branchId.Value))
            {
                BranchId = branchId.Value;
            }
            else if (!BranchId.HasValue || !ManagedBranchesList.Any(b => b.Id == BranchId.Value))
            {
                BranchId = ManagedBranchesList.FirstOrDefault()?.Id;
            }

            CurrentBranch = ManagedBranchesList.FirstOrDefault(b => b.Id == BranchId);
        }

        private async Task LoadDataAsync()
        {
            int? selectedBranchId = BranchId;
            int selMonth = Month ?? DateTime.Now.Month;
            int selYear = Year ?? DateTime.Now.Year;

            var empQuery = _db.Employees
                .Include(e => e.Department)
                .Include(e => e.Designation)
                .Where(e => e.Status == "Active" && !e.NIC.StartsWith("DUTY") && e.NIC != "DUTY-ACC");

            if (selectedBranchId.HasValue)
            {
                empQuery = empQuery.Where(e => e.BranchId == selectedBranchId.Value);
            }

            Employees = await empQuery
                .OrderBy(e => e.FullName)
                .ToListAsync();

            DepartmentsList = Employees
                .Where(e => e.Department != null)
                .Select(e => e.Department!.Name)
                .Distinct()
                .OrderBy(d => d)
                .ToList();

            var bonusQuery = _db.PayrollBonuses
                .Include(b => b.Employee)
                    .ThenInclude(e => e!.Department)
                .Include(b => b.Employee)
                    .ThenInclude(e => e!.Designation)
                .Where(b => b.Employee != null && !b.Employee.NIC.StartsWith("DUTY") && b.Employee.NIC != "DUTY-ACC" && b.Employee.Status == "Active");

            if (selectedBranchId.HasValue)
            {
                bonusQuery = bonusQuery.Where(b => b.Employee != null && b.Employee.BranchId == selectedBranchId.Value);
            }

            Bonuses = await bonusQuery
                .OrderByDescending(b => b.Year)
                .ThenByDescending(b => b.Month)
                .ThenBy(b => b.Employee!.FullName)
                .ToListAsync();

            var activeMonthBonuses = Bonuses
                .Where(b => b.Month == selMonth && b.Year == selYear)
                .ToList();

            TotalThisMonth = activeMonthBonuses.Sum(b => b.Amount);
            ActiveBeneficiariesCount = activeMonthBonuses.Select(b => b.EmployeeId).Distinct().Count();
            AverageAllowancePerBeneficiary = ActiveBeneficiariesCount > 0 
                ? TotalThisMonth / ActiveBeneficiariesCount 
                : 0;

            var topCatGroup = activeMonthBonuses
                .GroupBy(b => b.BonusType)
                .OrderByDescending(g => g.Sum(x => x.Amount))
                .FirstOrDefault();

            TopAllowanceCategory = topCatGroup != null 
                ? $"{topCatGroup.Key} (Rs {topCatGroup.Sum(x => x.Amount):N0})" 
                : "None";
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
