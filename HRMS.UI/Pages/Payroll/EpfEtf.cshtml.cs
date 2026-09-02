using HRMS.Domain.Entities.Core;
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
    public class EpfEtfModel : BasePageModel
    {
        public EpfEtfModel(ApplicationDbContext db) : base(db) { }

        public List<EpfEtfRow> SalaryData { get; set; } = new();
        public List<string> DepartmentsList { get; set; } = new();
        public decimal TotalBasicSalary { get; set; }
        public decimal TotalGrossSalary { get; set; }
        public decimal TotalEpfEmployee { get; set; }
        public decimal TotalEpfEmployer { get; set; }
        public decimal TotalEpfCombined => TotalEpfEmployee + TotalEpfEmployer;
        public decimal TotalEtf { get; set; }
        public decimal TotalStatutoryCost => TotalEpfCombined + TotalEtf;

        public List<Branch> ManagedBranchesList { get; set; } = new();
        [BindProperty(SupportsGet = true)] public int? BranchId { get; set; }
        public Branch? CurrentBranch { get; set; }

        [BindProperty(SupportsGet = true)] public int? Month { get; set; }
        [BindProperty(SupportsGet = true)] public int? Year { get; set; }
        public string SelectedMonthName { get; set; } = string.Empty;

        public async Task<IActionResult> OnGetAsync(int? branchId, int? month, int? year)
        {
            if (User.IsInRole("Admin"))
            {
                return Forbid();
            }

            await LoadCurrentUserAsync();

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
            int? selectedBranchId = BranchId;

            var now = DateTime.Now;
            int targetMonth = month ?? Month ?? now.Month;
            int targetYear = year ?? Year ?? now.Year;
            Month = targetMonth;
            Year = targetYear;
            SelectedMonthName = new DateTime(targetYear, targetMonth, 1).ToString("MMMM yyyy");

            var salaryQuery = _db.PayrollSalaries
                .Include(s => s.Employee)
                    .ThenInclude(e => e!.Department)
                .Include(s => s.Employee)
                    .ThenInclude(e => e!.Designation)
                .Include(s => s.Employee)
                    .ThenInclude(e => e!.Branch)
                .Where(s => s.Employee != null && !s.Employee.NIC.StartsWith("DUTY") && s.Employee.NIC != "DUTY-ACC" && s.Employee.Status == "Active");

            if (selectedBranchId.HasValue)
            {
                salaryQuery = salaryQuery.Where(s => s.Employee!.BranchId == selectedBranchId.Value);
            }

            var allSalaries = await salaryQuery.ToListAsync();

            var latestSalaries = allSalaries
                .GroupBy(s => s.EmployeeId)
                .Select(g => g.OrderByDescending(s => s.EffectiveDate).ThenByDescending(s => s.Id).First())
                .ToList();

            SalaryData = latestSalaries.Select(s => new EpfEtfRow
            {
                Employee = s.Employee,
                BasicSalary = s.BasicSalary,
                HousingAllowance = s.HousingAllowance,
                TransportAllowance = s.TransportAllowance,
                MedicalAllowance = s.MedicalAllowance,
                EpfEmployee = Math.Round(s.BasicSalary * 0.08m, 2),
                EpfEmployer = Math.Round(s.BasicSalary * 0.12m, 2),
                Etf = Math.Round(s.BasicSalary * 0.03m, 2)
            }).OrderBy(r => r.Employee?.FullName).ToList();

            TotalBasicSalary = SalaryData.Sum(r => r.BasicSalary);
            TotalGrossSalary = SalaryData.Sum(r => r.GrossSalary);
            TotalEpfEmployee = SalaryData.Sum(r => r.EpfEmployee);
            TotalEpfEmployer = SalaryData.Sum(r => r.EpfEmployer);
            TotalEtf = SalaryData.Sum(r => r.Etf);

            DepartmentsList = SalaryData
                .Where(r => r.Employee?.Department != null)
                .Select(r => r.Employee!.Department!.Name)
                .Distinct()
                .OrderBy(n => n)
                .ToList();

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

    public class EpfEtfRow
    {
        public HRMS.Domain.Entities.Core.Employee? Employee { get; set; }
        public decimal BasicSalary { get; set; }
        public decimal HousingAllowance { get; set; }
        public decimal TransportAllowance { get; set; }
        public decimal MedicalAllowance { get; set; }
        public decimal GrossSalary => BasicSalary + HousingAllowance + TransportAllowance + MedicalAllowance;
        public decimal EpfEmployee { get; set; }
        public decimal EpfEmployer { get; set; }
        public decimal TotalEpf => EpfEmployee + EpfEmployer;
        public decimal Etf { get; set; }
        public decimal TotalStatutory => TotalEpf + Etf;
        public decimal NetPayable => GrossSalary - EpfEmployee;
    }
}
