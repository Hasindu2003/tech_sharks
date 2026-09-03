using HRMS.Application.Services;
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
    public class PayslipPdfModel : BasePageModel
    {
        public PayslipPdfModel(ApplicationDbContext db) : base(db) { }

        public Payslip? Payslip { get; set; }

        public decimal BonusTotal { get; set; }
        public List<PayrollBonus> BonusDetails { get; set; } = new();

        public List<WelfarePayrollItem> WelfareAdditions { get; set; } = new();
        public List<WelfarePayrollItem> WelfareDeductions { get; set; } = new();
        public decimal WelfareAdditionsTotal { get; set; }
        public decimal WelfareDeductionsTotal { get; set; }

        public decimal AdjustedGrossPay { get; set; }
        public decimal AdjustedNetPay { get; set; }
        public decimal AdjustedTotalDeductions { get; set; }

        public async Task<IActionResult> OnGetAsync(int? id, [FromQuery(Name = "id")] int? queryId)
        {
            if (User.IsInRole("Admin"))
            {
                return Forbid();
            }

            int targetId = id ?? queryId ?? 0;
            if (targetId <= 0)
            {
                return NotFound();
            }

            await LoadCurrentUserAsync();

            Payslip = await _db.Payslips
                .Include(p => p.Employee)
                    .ThenInclude(e => e!.Designation)
                .Include(p => p.Employee)
                    .ThenInclude(e => e!.Department)
                .Include(p => p.PayrollRun)
                .FirstOrDefaultAsync(p => p.Id == targetId && p.Employee != null && !p.Employee.NIC.StartsWith("DUTY") && p.Employee.NIC != "DUTY-ACC");

            if (Payslip == null)
                return NotFound();

            bool isCorporate = User.IsInRole("HR Manager") || User.IsInRole("HR Officer");
            if (!isCorporate)
            {
                var username = User.Identity?.Name;
                var userAccount = await _db.Users.FirstOrDefaultAsync(u => u.UserName == username || u.Email == username);
                Employee? employee = null;
                if (userAccount?.EmployeeId.HasValue == true)
                {
                    employee = await _db.Employees
                        .Include(e => e.Branch)
                        .FirstOrDefaultAsync(e => e.Id == userAccount.EmployeeId.Value && !e.NIC.StartsWith("DUTY") && e.NIC != "DUTY-ACC");
                }
                if (employee == null && !string.IsNullOrEmpty(userAccount?.Email))
                {
                    employee = await _db.Employees
                        .Include(e => e.Branch)
                        .FirstOrDefaultAsync(e => e.Email == userAccount.Email && !e.NIC.StartsWith("DUTY") && e.NIC != "DUTY-ACC");
                }
                if (employee == null && !string.IsNullOrEmpty(username))
                {
                    employee = await _db.Employees
                        .Include(e => e.Branch)
                        .FirstOrDefaultAsync(e => e.Email == username && !e.NIC.StartsWith("DUTY") && e.NIC != "DUTY-ACC");
                }

                bool hasAccess = false;
                if (employee != null && Payslip.EmployeeId == employee.Id)
                {
                    hasAccess = true;
                }
                else if (User.IsInRole("Branch Manager"))
                {
                    int? bmBranchId = employee?.BranchId;
                    if (!bmBranchId.HasValue && !string.IsNullOrWhiteSpace(userAccount?.Branch))
                    {
                        var b = await _db.Branches.FirstOrDefaultAsync(x => x.Name.ToLower() == userAccount.Branch.ToLower());
                        bmBranchId = b?.Id;
                    }
                    if (bmBranchId.HasValue && Payslip.Employee?.BranchId == bmBranchId.Value)
                    {
                        hasAccess = true;
                    }
                }
                else if (User.IsInRole("Area Manager"))
                {
                    var allowedIds = ParseManagedBranches(userAccount?.ManagedBranches);
                    if (allowedIds != null && Payslip.Employee?.BranchId != null && allowedIds.Contains(Payslip.Employee.BranchId))
                    {
                        hasAccess = true;
                    }
                }

                if (!hasAccess)
                    return Forbid();
            }

            if (Payslip.PayrollRun != null)
            {
                BonusDetails = await _db.PayrollBonuses
                    .Where(b => b.EmployeeId == Payslip.EmployeeId
                             && b.Month == Payslip.PayrollRun.Month
                             && b.Year == Payslip.PayrollRun.Year)
                    .ToListAsync();

                BonusTotal = BonusDetails.Sum(b => b.Amount);

                var allWelfare = await _db.WelfareRequests
                    .Include(w => w.WelfareType)
                    .Where(w => w.EmployeeId == Payslip.EmployeeId && (w.Status == "Paid" || w.CurrentStatus == "PaymentCompleted" || w.Status == "Approved"))
                    .ToListAsync();

                WelfareAdditions = WelfarePayrollHelper.GetWelfareAdditions(allWelfare, Payslip.EmployeeId, Payslip.PayrollRun.Month, Payslip.PayrollRun.Year);
                WelfareDeductions = WelfarePayrollHelper.GetWelfareDeductions(allWelfare, Payslip.EmployeeId, Payslip.PayrollRun.Month, Payslip.PayrollRun.Year);

                WelfareAdditionsTotal = WelfareAdditions.Sum(w => w.Amount);
                WelfareDeductionsTotal = WelfareDeductions.Sum(w => w.Amount);
            }

            var totalBonuses = BonusTotal > 0 ? BonusTotal : Payslip.Bonuses;
            AdjustedGrossPay = Payslip.BasicSalary + totalBonuses + WelfareAdditionsTotal;

            var epfEmployee = Payslip.EpfEmployee > 0 ? Payslip.EpfEmployee : Math.Round(Payslip.BasicSalary * 0.08m, 2);
            var taxDeduction = Payslip.TaxDeduction > 0 ? Payslip.TaxDeduction : TaxCalculationService.CalculateMonthlyApitTax(AdjustedGrossPay);

            AdjustedTotalDeductions = epfEmployee + taxDeduction + WelfareDeductionsTotal;
            AdjustedNetPay = AdjustedGrossPay - AdjustedTotalDeductions;

            if (Payslip.Bonuses != totalBonuses || Payslip.GrossPay != AdjustedGrossPay || Payslip.NetPay != AdjustedNetPay || Payslip.TotalDeductions != AdjustedTotalDeductions || Payslip.MedicalAllowance != WelfareAdditionsTotal || Payslip.TaxDeduction != taxDeduction)
            {
                Payslip.Bonuses = totalBonuses;
                Payslip.MedicalAllowance = WelfareAdditionsTotal;
                Payslip.GrossPay = AdjustedGrossPay;
                Payslip.TaxDeduction = taxDeduction;
                Payslip.TotalDeductions = AdjustedTotalDeductions;
                Payslip.NetPay = AdjustedNetPay;
                Payslip.EpfEmployee = epfEmployee;

                await _db.SaveChangesAsync();
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
