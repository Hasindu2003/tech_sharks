using HRMS.Domain.Entities.Core;
using HRMS.Domain.Entities.Payroll;
using HRMS.Domain.Common;
using HRMS.Infrastructure.Identity;
using HRMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HRMS.UI.Pages.Payroll
{
    [Authorize(Roles = "HR Manager,HR Officer")]
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public IndexModel(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public List<Branch> ManagedBranchesList { get; set; } = new();
        [BindProperty(SupportsGet = true)] public int? BranchId { get; set; }
        public Branch? CurrentBranch { get; set; }

        public string CurrentMonthName { get; set; } = string.Empty;
        public decimal TotalPayroll { get; set; }
        public decimal LastMonthPayroll { get; set; }
        public string LastMonthName { get; set; } = string.Empty;
        public int TotalEmployees { get; set; }
        public decimal TotalBonuses { get; set; }
        public decimal TotalDeductions { get; set; }
        public decimal TotalEmployerContributions { get; set; }
        public List<PayrollRun> PastRuns { get; set; } = new();

        public bool HasSalaryRecords { get; set; }
        public bool HasBonusRecords { get; set; }
        public bool HasPayrollRun { get; set; }
        public bool HasPayslips { get; set; }
        public int AnomalyCount { get; set; }

        // OT Policy Settings
        public PayrollPolicySetting OtPolicy { get; set; } = new();

        public async Task<IActionResult> OnGetAsync(int? branchId)
        {
            if (User.IsInRole("Admin"))
            {
                return Forbid();
            }

            var currentUser = await _userManager.GetUserAsync(User);
            if (User.IsInRole("HR Officer"))
            {
                var allowedIds = ParseManagedBranches(currentUser?.ManagedBranches);
                if (allowedIds != null && allowedIds.Any())
                {
                    ManagedBranchesList = await _context.Branches
                        .Where(b => allowedIds.Contains(b.Id))
                        .OrderBy(b => b.Name)
                        .ToListAsync();
                }
                else
                {
                    ManagedBranchesList = await _context.Branches.OrderBy(b => b.Name).ToListAsync();
                }
            }
            else
            {
                ManagedBranchesList = await _context.Branches.OrderBy(b => b.Name).ToListAsync();
            }

            if (!ManagedBranchesList.Any())
            {
                ManagedBranchesList = await _context.Branches.OrderBy(b => b.Name).ToListAsync();
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
            int selectedBranchId = BranchId ?? 0;

            var now = SriLankaTime.Now;
            CurrentMonthName = now.ToString("MMMM yyyy");

            var lastMonthDate = now.AddMonths(-1);
            LastMonthName = lastMonthDate.ToString("MMM yyyy");

            // Load OT policy (branch-specific first, then global fallback)
            OtPolicy = await _context.PayrollPolicySettings
                .FirstOrDefaultAsync(p => p.BranchId == selectedBranchId)
                ?? await _context.PayrollPolicySettings
                    .FirstOrDefaultAsync(p => p.BranchId == null)
                ?? new PayrollPolicySetting();

            TotalEmployees = await _context.PayrollSalaries
                .Include(s => s.Employee)
                .Where(s => s.Employee != null && !s.Employee.NIC.StartsWith("DUTY") && s.Employee.NIC != "DUTY-ACC" && s.Employee.BranchId == selectedBranchId && s.Employee.Status == "Active")
                .Select(s => s.EmployeeId)
                .Distinct()
                .CountAsync();

            var salaries = await _context.PayrollSalaries
                .Include(s => s.Employee)
                .Where(s => s.Employee != null && !s.Employee.NIC.StartsWith("DUTY") && s.Employee.NIC != "DUTY-ACC" && s.Employee.BranchId == selectedBranchId && s.Employee.Status == "Active")
                .GroupBy(s => s.EmployeeId)
                .Select(g => g.OrderByDescending(s => s.EffectiveDate).ThenByDescending(s => s.Id).First())
                .ToListAsync();

            TotalPayroll = salaries.Sum(s => s.BasicSalary);

            TotalBonuses = await _context.PayrollBonuses
                .Include(b => b.Employee)
                .Where(b => b.Month == now.Month && b.Year == now.Year && b.Employee != null && b.Employee.BranchId == selectedBranchId && b.Employee.Status == "Active")
                .SumAsync(b => (decimal?)b.Amount) ?? 0;

            TotalDeductions = salaries.Sum(s => Math.Round(s.BasicSalary * 0.08m, 2));
            TotalEmployerContributions = salaries.Sum(s => Math.Round(s.BasicSalary * 0.15m, 2));

            var lastMonth = await _context.PayrollRuns
                .Where(r => r.Month == lastMonthDate.Month && r.Year == lastMonthDate.Year && (r.BranchId == selectedBranchId || r.BranchId == null))
                .FirstOrDefaultAsync();
            LastMonthPayroll = lastMonth?.TotalAmount ?? 0;

            PastRuns = await _context.PayrollRuns
                .Include(r => r.Branch)
                .Where(r => r.Status == "Completed" && (r.BranchId == selectedBranchId || r.BranchId == null))
                .OrderByDescending(r => r.Year)
                .ThenByDescending(r => r.Month)
                .Take(5)
                .ToListAsync();

            HasSalaryRecords = salaries.Any();
            HasBonusRecords = await _context.PayrollBonuses
                .Include(b => b.Employee)
                .AnyAsync(b => b.Month == now.Month && b.Year == now.Year && b.Employee != null && b.Employee.BranchId == selectedBranchId && b.Employee.Status == "Active");

            HasPayrollRun = await _context.PayrollRuns
                .AnyAsync(r => r.Month == now.Month && r.Year == now.Year && r.BranchId == selectedBranchId);

            HasPayslips = await _context.Payslips
                .Include(p => p.Employee)
                .Include(p => p.PayrollRun)
                .AnyAsync(p => p.Employee != null && p.Employee.BranchId == selectedBranchId && p.Employee.Status == "Active" && p.PayrollRun != null && p.PayrollRun.Month == now.Month && p.PayrollRun.Year == now.Year);

            var startOfMonth = new DateTime(now.Year, now.Month, 1);
            var endOfMonth = startOfMonth.AddMonths(1).AddDays(-1);
            AnomalyCount = await _context.Attendances
                .Include(a => a.Employee)
                .CountAsync(a => a.Date >= startOfMonth && a.Date <= endOfMonth 
                              && a.Status == "Anomaly" 
                              && a.Employee != null && !a.Employee.NIC.StartsWith("DUTY") && a.Employee.NIC != "DUTY-ACC"
                              && a.Employee.BranchId == selectedBranchId
                              && a.Employee.Status == "Active");

            return Page();
        }

        public async Task<IActionResult> OnPostRunPayrollAsync(int branchId)
        {
            if (User.IsInRole("Admin"))
            {
                return Forbid();
            }

            var now = SriLankaTime.Now;

            var branch = await _context.Branches.FindAsync(branchId);
            var branchName = branch?.Name ?? "Selected Branch";

            var existing = await _context.PayrollRuns
                .FirstOrDefaultAsync(r => r.Month == now.Month && r.Year == now.Year && r.BranchId == branchId);

            if (existing != null)
            {
                TempData["Success"] = $"Payroll for {branchName} ({now:MMMM yyyy}) has already been processed.";
                return RedirectToPage(new { branchId = branchId });
            }

            // Load OT policy
            var policy = await _context.PayrollPolicySettings
                .FirstOrDefaultAsync(p => p.BranchId == branchId)
                ?? await _context.PayrollPolicySettings
                    .FirstOrDefaultAsync(p => p.BranchId == null)
                ?? new PayrollPolicySetting();

            var salaries = await _context.PayrollSalaries
                .Include(s => s.Employee)
                .Where(s => s.Employee != null && !s.Employee.NIC.StartsWith("DUTY") && s.Employee.NIC != "DUTY-ACC" && s.Employee.BranchId == branchId && s.Employee.Status == "Active")
                .GroupBy(s => s.EmployeeId)
                .Select(g => g.OrderByDescending(s => s.EffectiveDate).ThenByDescending(s => s.Id).First())
                .ToListAsync();

            if (!salaries.Any())
            {
                TempData["Success"] = $"No active employee salary records found for {branchName}. Please configure employee salaries first.";
                return RedirectToPage(new { branchId = branchId });
            }

            // Auto-calculate OT from attendance if policy enabled
            if (policy.AutoCalculateOtOnPayroll)
            {
                var startOfMonth = new DateTime(now.Year, now.Month, 1);
                var nextMonth = startOfMonth.AddMonths(1);

                var attendances = await _context.Attendances
                    .Where(a => a.Date >= startOfMonth && a.Date < nextMonth)
                    .ToListAsync();

                var monthlyBaseHours = policy.StandardMonthlyWorkingDays * policy.StandardDailyWorkingHours;

                // Remove any previously auto-calculated OT bonuses for this month/branch
                var existingOtBonuses = await _context.PayrollBonuses
                    .Include(b => b.Employee)
                    .Where(b => b.Month == now.Month && b.Year == now.Year && b.BonusType == "Overtime" && b.Employee != null && b.Employee.BranchId == branchId)
                    .ToListAsync();
                if (existingOtBonuses.Any())
                {
                    _context.PayrollBonuses.RemoveRange(existingOtBonuses);
                }

                foreach (var sal in salaries)
                {
                    var empAtt = attendances.Where(a => a.EmployeeId == sal.EmployeeId).ToList();

                    decimal weekdayOtHours = 0;
                    decimal weekendOtHours = 0;

                    foreach (var att in empAtt)
                    {
                        double workedHours = 0;
                        if (att.TotalHours.HasValue && att.TotalHours.Value > 0)
                        {
                            workedHours = att.TotalHours.Value;
                        }
                        else if (att.TimeIn.HasValue && att.TimeOut.HasValue)
                        {
                            workedHours = (att.TimeOut.Value - att.TimeIn.Value).TotalHours;
                        }

                        bool isWeekend = att.Date.DayOfWeek == DayOfWeek.Saturday || att.Date.DayOfWeek == DayOfWeek.Sunday;

                        if (isWeekend)
                        {
                            if (workedHours > 0)
                            {
                                weekendOtHours += (decimal)Math.Floor(workedHours);
                            }
                        }
                        else
                        {
                            double dailyLimit = (double)policy.StandardDailyWorkingHours;
                            if (workedHours > dailyLimit)
                            {
                                weekdayOtHours += (decimal)Math.Floor(workedHours - dailyLimit);
                            }
                        }
                    }

                    decimal totalOtHours = weekdayOtHours + weekendOtHours;

                    if (totalOtHours > 0 && monthlyBaseHours > 0)
                    {
                        var hourlyRate = sal.BasicSalary / monthlyBaseHours;
                        var regularOtRate = Math.Round(hourlyRate * policy.StandardOtMultiplier, 2);
                        var weekendOtRate = Math.Round(hourlyRate * policy.WeekendOtMultiplier, 2);

                        var regularOtPay = Math.Round(weekdayOtHours * regularOtRate, 2);
                        var weekendOtPay = Math.Round(weekendOtHours * weekendOtRate, 2);
                        var totalOtPay = regularOtPay + weekendOtPay;

                        string reason;
                        if (weekdayOtHours > 0 && weekendOtHours > 0)
                        {
                            reason = $"Overtime — Weekday: {weekdayOtHours} hrs (Rs {regularOtRate:N2}) + Weekend: {weekendOtHours} hrs (Rs {weekendOtRate:N2})";
                        }
                        else if (weekendOtHours > 0)
                        {
                            reason = $"Overtime (Weekend) — {weekendOtHours} hrs @ Rs {weekendOtRate:N2}/hr";
                        }
                        else
                        {
                            reason = $"Overtime — {weekdayOtHours} hrs @ Rs {regularOtRate:N2}/hr";
                        }

                        _context.PayrollBonuses.Add(new PayrollBonus
                        {
                            EmployeeId = sal.EmployeeId,
                            BonusType = "Overtime",
                            Amount = totalOtPay,
                            Month = now.Month,
                            Year = now.Year,
                            Reason = reason
                        });
                    }
                }

                await _context.SaveChangesAsync();
            }

            // Reload bonuses (now includes auto-generated OT)
            var bonuses = await _context.PayrollBonuses
                .Include(b => b.Employee)
                .Where(b => b.Month == now.Month && b.Year == now.Year && b.Employee != null && b.Employee.BranchId == branchId && b.Employee.Status == "Active")
                .ToListAsync();

            var run = new PayrollRun
            {
                Month = now.Month,
                Year = now.Year,
                BranchId = branchId,
                Status = "Completed",
                ProcessedAt = SriLankaTime.Now,
                TotalEmployees = salaries.Count
            };

            _context.PayrollRuns.Add(run);
            await _context.SaveChangesAsync();

            decimal totalAmount = 0;

            foreach (var sal in salaries)
            {
                var empBonus = bonuses.Where(b => b.EmployeeId == sal.EmployeeId).Sum(b => b.Amount);
                var grossPay = sal.BasicSalary + empBonus;
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
                    HousingAllowance = 0,
                    TransportAllowance = 0,
                    MedicalAllowance = 0,
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

            TempData["Success"] = $"Payroll for {branchName} ({now:MMMM yyyy}) processed! {salaries.Count} payslips generated.";
            return RedirectToPage(new { branchId = branchId });
        }

        public async Task<IActionResult> OnPostStartOverPayrollAsync(int? month, int? year, int branchId)
        {
            if (User.IsInRole("Admin"))
            {
                return Forbid();
            }

            var now = SriLankaTime.Now;
            int targetMonth = month ?? now.Month;
            int targetYear = year ?? now.Year;

            var branch = await _context.Branches.FindAsync(branchId);
            var branchName = branch?.Name ?? "Selected Branch";

            var runs = await _context.PayrollRuns
                .Where(r => r.Month == targetMonth && r.Year == targetYear && r.BranchId == branchId)
                .ToListAsync();

            var runIds = runs.Select(r => r.Id).ToList();

            var payslips = await _context.Payslips
                .Include(p => p.Employee)
                .Where(p => runIds.Contains(p.PayrollRunId) || 
                           (p.Employee != null && p.Employee.BranchId == branchId && p.PayrollRun != null && p.PayrollRun.Month == targetMonth && p.PayrollRun.Year == targetYear))
                .ToListAsync();

            if (payslips.Any())
            {
                _context.Payslips.RemoveRange(payslips);
            }

            if (runs.Any())
            {
                _context.PayrollRuns.RemoveRange(runs);
            }

            // Also remove auto-calculated OT bonuses when starting over
            var otBonuses = await _context.PayrollBonuses
                .Include(b => b.Employee)
                .Where(b => b.Month == targetMonth && b.Year == targetYear && b.BonusType == "Overtime" && b.Employee != null && b.Employee.BranchId == branchId)
                .ToListAsync();
            if (otBonuses.Any())
            {
                _context.PayrollBonuses.RemoveRange(otBonuses);
            }

            await _context.SaveChangesAsync();

            var monthName = new DateTime(targetYear, targetMonth, 1).ToString("MMMM yyyy");
            TempData["Success"] = $"Payroll cycle for {branchName} ({monthName}) has been reset. You can now adjust records and run payroll again.";
            TempData["ResetChecklist"] = "true";

            return RedirectToPage(new { branchId = branchId });
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
