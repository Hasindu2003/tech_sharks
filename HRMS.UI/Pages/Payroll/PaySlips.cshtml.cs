using HRMS.Application.Services;
using HRMS.Domain.Entities.Core;
using HRMS.Domain.Entities.Payroll;
using HRMS.Domain.Entities.Welfare;
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
    using Employee = HRMS.Domain.Entities.Core.Employee;

    [Authorize]
    public class PayslipsModel : BasePageModel
    {
        public PayslipsModel(ApplicationDbContext db) : base(db) { }

        public List<Payslip> Payslips { get; set; } = new();
        public List<PayrollRun> PayrollRuns { get; set; } = new();
        public List<PayrollBonus> AllBonuses { get; set; } = new();
        public List<WelfareRequest> AllWelfareRequests { get; set; } = new();
        public bool IsEmployee { get; set; }

        public List<Branch> ManagedBranchesList { get; set; } = new();
        [BindProperty(SupportsGet = true)] public int? BranchId { get; set; }
        public Branch? CurrentBranch { get; set; }

        public async Task<IActionResult> OnGetAsync(int? branchId)
        {
            if (User.IsInRole("Admin"))
            {
                return Forbid();
            }

            await LoadCurrentUserAsync();

            bool isCorporate = User.IsInRole("HR Manager") || User.IsInRole("HR Officer");
            bool isBranchManager = User.IsInRole("Branch Manager");
            bool isAreaManager = User.IsInRole("Area Manager");
            bool isManagerOrCorporate = isCorporate || isBranchManager || isAreaManager;
            IsEmployee = !isManagerOrCorporate;

            if (isManagerOrCorporate)
            {
                var username = User.Identity?.Name;
                var userAccount = await _db.Users.FirstOrDefaultAsync(u => u.UserName == username || u.Email == username);

                if (User.IsInRole("HR Manager"))
                {
                    ManagedBranchesList = await _db.Branches.OrderBy(b => b.Name).ToListAsync();
                }
                else if (User.IsInRole("HR Officer") || User.IsInRole("Area Manager"))
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
                else if (User.IsInRole("Branch Manager"))
                {
                    Branch? branch = null;
                    if (userAccount?.EmployeeId.HasValue == true)
                    {
                        var emp = await _db.Employees.Include(e => e.Branch).FirstOrDefaultAsync(e => e.Id == userAccount.EmployeeId.Value);
                        branch = emp?.Branch;
                    }
                    if (branch == null && !string.IsNullOrWhiteSpace(userAccount?.Branch))
                    {
                        branch = await _db.Branches.FirstOrDefaultAsync(b => b.Name.ToLower() == userAccount.Branch.ToLower());
                    }
                    if (branch != null)
                    {
                        ManagedBranchesList = new List<Branch> { branch };
                    }
                    else
                    {
                        ManagedBranchesList = await _db.Branches.OrderBy(b => b.Name).ToListAsync();
                    }
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

                PayrollRuns = await _db.PayrollRuns
                    .Include(r => r.Branch)
                    .Where(r => r.Status == "Completed" && (r.BranchId == selectedBranchId || r.BranchId == null))
                    .OrderByDescending(r => r.Year)
                    .ThenByDescending(r => r.Month)
                    .ToListAsync();

                var payslipQuery = _db.Payslips
                    .Include(p => p.Employee)
                        .ThenInclude(e => e!.Designation)
                    .Include(p => p.Employee)
                        .ThenInclude(e => e!.Branch)
                    .Include(p => p.PayrollRun)
                    .Where(p => p.Employee != null && !p.Employee.NIC.StartsWith("DUTY") && p.Employee.NIC != "DUTY-ACC");

                if (selectedBranchId.HasValue)
                {
                    payslipQuery = payslipQuery.Where(p => p.Employee!.BranchId == selectedBranchId.Value);
                }

                Payslips = await payslipQuery
                    .OrderByDescending(p => p.PayrollRun!.Year)
                    .ThenByDescending(p => p.PayrollRun!.Month)
                    .ThenBy(p => p.Employee!.FullName)
                    .ToListAsync();
            }
            else
            {
                var username = User.Identity?.Name;
                var userAccount = await _db.Users.FirstOrDefaultAsync(u => u.UserName == username || u.Email == username);
                Employee? employee = null;
                if (userAccount?.EmployeeId.HasValue == true)
                {
                    employee = await _db.Employees
                        .FirstOrDefaultAsync(e => e.Id == userAccount.EmployeeId.Value && !e.NIC.StartsWith("DUTY") && e.NIC != "DUTY-ACC");
                }
                if (employee == null && !string.IsNullOrEmpty(userAccount?.Email))
                {
                    employee = await _db.Employees
                        .FirstOrDefaultAsync(e => e.Email == userAccount.Email && !e.NIC.StartsWith("DUTY") && e.NIC != "DUTY-ACC");
                }
                if (employee == null && !string.IsNullOrEmpty(username))
                {
                    employee = await _db.Employees
                        .FirstOrDefaultAsync(e => e.Email == username && !e.NIC.StartsWith("DUTY") && e.NIC != "DUTY-ACC");
                }

                PayrollRuns = await _db.PayrollRuns
                    .Where(r => r.Status == "Completed")
                    .OrderByDescending(r => r.Year)
                    .ThenByDescending(r => r.Month)
                    .ToListAsync();

                if (employee != null)
                {
                    Payslips = await _db.Payslips
                        .Include(p => p.Employee)
                            .ThenInclude(e => e!.Designation)
                        .Include(p => p.PayrollRun)
                        .Where(p => p.EmployeeId == employee.Id)
                        .OrderByDescending(p => p.PayrollRun!.Year)
                        .ThenByDescending(p => p.PayrollRun!.Month)
                        .ToListAsync();
                }
            }

            var empIds = Payslips.Select(p => p.EmployeeId).Distinct().ToList();
            if (empIds.Any())
            {
                AllBonuses = await _db.PayrollBonuses
                    .Where(b => empIds.Contains(b.EmployeeId))
                    .ToListAsync();

                AllWelfareRequests = await _db.WelfareRequests
                    .Include(w => w.WelfareType)
                    .Where(w => empIds.Contains(w.EmployeeId) && (w.Status == "Paid" || w.CurrentStatus == "PaymentCompleted" || w.Status == "Approved"))
                    .ToListAsync();

                bool dbChanged = false;
                foreach (var p in Payslips)
                {
                    if (p.PayrollRun == null) continue;

                    var itemized = AllBonuses
                        .Where(b => b.EmployeeId == p.EmployeeId 
                                 && b.Month == p.PayrollRun.Month 
                                 && b.Year == p.PayrollRun.Year)
                        .ToList();

                    var welfareAdditions = WelfarePayrollHelper.GetWelfareAdditions(AllWelfareRequests, p.EmployeeId, p.PayrollRun.Month, p.PayrollRun.Year);
                    var welfareDeductions = WelfarePayrollHelper.GetWelfareDeductions(AllWelfareRequests, p.EmployeeId, p.PayrollRun.Month, p.PayrollRun.Year);

                    decimal bonusSum = itemized.Any() ? itemized.Sum(b => b.Amount) : p.Bonuses;
                    decimal welfareAddSum = welfareAdditions.Sum(w => w.Amount);
                    decimal welfareDedSum = welfareDeductions.Sum(w => w.Amount);

                    decimal epfEmp = Math.Round(p.BasicSalary * 0.08m, 2);
                    decimal epfCo = Math.Round(p.BasicSalary * 0.12m, 2);
                    decimal etf = Math.Round(p.BasicSalary * 0.03m, 2);
                    decimal gross = p.BasicSalary + bonusSum + welfareAddSum;
                    decimal tax = TaxCalculationService.CalculateMonthlyApitTax(gross);
                    decimal ded = epfEmp + tax + welfareDedSum;
                    decimal net = gross - ded;

                    if (p.Bonuses != bonusSum || p.GrossPay != gross || p.TotalDeductions != ded || p.NetPay != net || p.MedicalAllowance != welfareAddSum || p.EpfEmployee != epfEmp || p.EpfEmployer != epfCo || p.Etf != etf || p.TaxDeduction != tax)
                    {
                        p.Bonuses = bonusSum;
                        p.HousingAllowance = 0;
                        p.TransportAllowance = 0;
                        p.MedicalAllowance = welfareAddSum;
                        p.GrossPay = gross;
                        p.TaxDeduction = tax;
                        p.TotalDeductions = ded;
                        p.NetPay = net;
                        p.EpfEmployee = epfEmp;
                        p.EpfEmployer = epfCo;
                        p.Etf = etf;
                        dbChanged = true;
                    }
                }

                if (dbChanged)
                {
                    await _db.SaveChangesAsync();
                }
            }

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
