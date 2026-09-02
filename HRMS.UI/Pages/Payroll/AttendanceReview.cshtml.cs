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
    public class AttendanceReviewModel : BasePageModel
    {
        public AttendanceReviewModel(ApplicationDbContext db) : base(db) { }

        public List<AttendanceRecord> AttendanceData { get; set; } = new();
        public List<string> BranchDepartments { get; set; } = new();
        public List<Branch> ManagedBranchesList { get; set; } = new();
        [BindProperty(SupportsGet = true)] public int? BranchId { get; set; }
        public Branch? CurrentBranch { get; set; }

        [BindProperty(SupportsGet = true)] public int? Month { get; set; }
        [BindProperty(SupportsGet = true)] public int? Year { get; set; }
        public string SelectedMonthName { get; set; } = string.Empty;

        // OT Policy for display
        public PayrollPolicySetting OtPolicy { get; set; } = new();

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
            int? targetBranchId = BranchId;

            // Resolve target Month and Year
            var now = DateTime.Now;
            int targetMonth = month ?? Month ?? now.Month;
            int targetYear = year ?? Year ?? now.Year;
            Month = targetMonth;
            Year = targetYear;
            SelectedMonthName = new DateTime(targetYear, targetMonth, 1).ToString("MMMM yyyy");

            // Load OT policy (branch-specific first, then global fallback)
            int selectedBranchId = BranchId ?? 0;
            OtPolicy = await _db.PayrollPolicySettings
                .FirstOrDefaultAsync(p => p.BranchId == selectedBranchId)
                ?? await _db.PayrollPolicySettings
                    .FirstOrDefaultAsync(p => p.BranchId == null)
                ?? new PayrollPolicySetting();

            // Fetch departments for this branch
            if (targetBranchId.HasValue)
            {
                BranchDepartments = await _db.BranchDepartments
                    .Where(bd => bd.BranchId == targetBranchId.Value)
                    .Include(bd => bd.Department)
                    .Select(bd => bd.Department.Name)
                    .Distinct()
                    .OrderBy(name => name)
                    .ToListAsync();
            }
            else
            {
                BranchDepartments = await _db.Departments
                    .Select(d => d.Name)
                    .Distinct()
                    .OrderBy(name => name)
                    .ToListAsync();
            }

            var startOfMonth = new DateTime(targetYear, targetMonth, 1, 0, 0, 0, DateTimeKind.Unspecified);
            var nextMonth = startOfMonth.AddMonths(1);
            var lastDayOfMonth = nextMonth.AddDays(-1);

            int totalWeekdays = 0;
            for (var date = startOfMonth; date <= lastDayOfMonth; date = date.AddDays(1))
            {
                if (date.DayOfWeek != DayOfWeek.Saturday && date.DayOfWeek != DayOfWeek.Sunday)
                {
                    totalWeekdays++;
                }
            }

            var employeeQuery = _db.Employees
                .Include(e => e.Department)
                .Include(e => e.Designation)
                .Where(e => e.Status == "Active" && !e.NIC.StartsWith("DUTY") && e.NIC != "DUTY-ACC");

            if (targetBranchId.HasValue)
            {
                employeeQuery = employeeQuery.Where(e => e.BranchId == targetBranchId.Value);
            }

            var employees = await employeeQuery.ToListAsync();

            // Fetch latest salary for each employee to calculate OT pay
            var salaryMap = new Dictionary<int, decimal>();
            var salaryRecords = await _db.PayrollSalaries
                .Include(s => s.Employee)
                .Where(s => s.Employee != null && s.Employee.Status == "Active" && !s.Employee.NIC.StartsWith("DUTY") && s.Employee.NIC != "DUTY-ACC")
                .ToListAsync();

            foreach (var emp in employees)
            {
                var latestSal = salaryRecords
                    .Where(s => s.EmployeeId == emp.Id)
                    .OrderByDescending(s => s.EffectiveDate)
                    .ThenByDescending(s => s.Id)
                    .FirstOrDefault();
                if (latestSal != null)
                {
                    salaryMap[emp.Id] = latestSal.BasicSalary;
                }
            }

            var monthlyBaseHours = OtPolicy.StandardMonthlyWorkingDays * OtPolicy.StandardDailyWorkingHours;

            // Fetch attendances strictly within this month range
            var attendances = await _db.Attendances
                .Where(a => a.Date >= startOfMonth && a.Date < nextMonth)
                .ToListAsync();

            // Fetch approved leaves overlapping with this month
            var leaves = await _db.Leaves
                .Where(l => l.Status == "Approved" && l.StartDate < nextMonth && l.EndDate >= startOfMonth)
                .ToListAsync();

            foreach (var emp in employees)
            {
                var empAtt = attendances.Where(a => a.EmployeeId == emp.Id).ToList();
                var empLeaves = leaves.Where(l => l.EmployeeId == emp.Id).ToList();

                int workingDays = empAtt
                    .Where(a => string.Equals(a.Status, "Present", StringComparison.OrdinalIgnoreCase) || 
                                string.Equals(a.Status, "Late", StringComparison.OrdinalIgnoreCase) || 
                                string.Equals(a.Status, "Verified", StringComparison.OrdinalIgnoreCase) ||
                                (a.TotalHours.HasValue && a.TotalHours.Value > 0) ||
                                (a.TimeIn.HasValue && a.TimeOut.HasValue))
                    .Select(a => a.Date.Date)
                    .Distinct()
                    .Count();

                // Map employee leaves across working days accurately
                var empLeaveDays = new Dictionary<DateTime, (string LeaveType, bool IsNoPay)>();
                foreach (var leave in empLeaves)
                {
                    var cur = leave.StartDate.Date;
                    var end = leave.EndDate.Date;
                    if (end < cur) end = cur;

                    bool isNoPay = string.Equals(leave.LeaveType, "No-Pay", StringComparison.OrdinalIgnoreCase) ||
                                   string.Equals(leave.LeaveType, "Unpaid", StringComparison.OrdinalIgnoreCase);

                    int daysRemaining = leave.TotalDays > 0 ? (int)Math.Ceiling(leave.TotalDays) : (end - cur).Days + 1;

                    while (cur <= end && daysRemaining > 0)
                    {
                        if (cur.DayOfWeek != DayOfWeek.Saturday && cur.DayOfWeek != DayOfWeek.Sunday)
                        {
                            if (cur >= startOfMonth && cur <= lastDayOfMonth)
                            {
                                empLeaveDays[cur] = (leave.LeaveType, isNoPay);
                            }
                            daysRemaining--;
                        }
                        cur = cur.AddDays(1);
                    }
                    while (daysRemaining > 0 && cur <= lastDayOfMonth)
                    {
                        if (cur.DayOfWeek != DayOfWeek.Saturday && cur.DayOfWeek != DayOfWeek.Sunday)
                        {
                            if (cur >= startOfMonth && cur <= lastDayOfMonth)
                            {
                                empLeaveDays[cur] = (leave.LeaveType, isNoPay);
                            }
                            daysRemaining--;
                        }
                        cur = cur.AddDays(1);
                    }
                }

                decimal weekdayOtHours = 0;
                decimal weekendOtHours = 0;
                var dailyLogs = new List<DailyLogItem>();

                for (var d = startOfMonth; d <= lastDayOfMonth; d = d.AddDays(1))
                {
                    var dayAtt = empAtt.FirstOrDefault(a => a.Date.Date == d.Date);
                    bool hasLeaveDay = empLeaveDays.TryGetValue(d.Date, out var lInfo);
                    bool isWeekend = d.DayOfWeek == DayOfWeek.Saturday || d.DayOfWeek == DayOfWeek.Sunday;

                    var log = new DailyLogItem
                    {
                        DateStr = d.ToString("MMM dd"),
                        DayName = d.ToString("ddd"),
                        IsWeekend = isWeekend,
                        HasLeave = hasLeaveDay,
                        LeaveType = hasLeaveDay ? lInfo.LeaveType : string.Empty
                    };

                    double worked = 0;
                    if (dayAtt != null)
                    {
                        log.TimeIn = dayAtt.TimeIn?.ToString("hh:mm tt") ?? "—";
                        log.TimeOut = dayAtt.TimeOut?.ToString("hh:mm tt") ?? "—";

                        if (dayAtt.TotalHours.HasValue && dayAtt.TotalHours.Value > 0)
                        {
                            worked = dayAtt.TotalHours.Value;
                        }
                        else if (dayAtt.TimeIn.HasValue && dayAtt.TimeOut.HasValue)
                        {
                            worked = (dayAtt.TimeOut.Value - dayAtt.TimeIn.Value).TotalHours;
                        }
                        log.TotalHours = Math.Round(worked, 1);
                    }

                    if (hasLeaveDay)
                    {
                        if (worked > 0)
                        {
                            log.Status = $"Present + Leave ({lInfo.LeaveType})";
                            log.StatusClass = "k-badge-warning";
                        }
                        else if (lInfo.IsNoPay)
                        {
                            log.Status = "No-Pay Leave";
                            log.StatusClass = "k-badge-rejected";
                        }
                        else
                        {
                            log.Status = $"Leave ({lInfo.LeaveType})";
                            log.StatusClass = "k-badge-warning";
                        }
                    }
                    else if (dayAtt != null && (worked > 0 || (dayAtt.TimeIn.HasValue && dayAtt.TimeOut.HasValue)))
                    {
                        if (string.Equals(dayAtt.Status, "Anomaly", StringComparison.OrdinalIgnoreCase) ||
                            (dayAtt.TimeIn.HasValue && !dayAtt.TimeOut.HasValue && d.Date < now.Date))
                        {
                            log.Status = "Anomaly (Missing Out)";
                            log.StatusClass = "k-badge-rejected";
                        }
                        else if (string.Equals(dayAtt.Status, "Late", StringComparison.OrdinalIgnoreCase))
                        {
                            log.Status = "Late";
                            log.StatusClass = "k-badge-warning";
                        }
                        else if (isWeekend)
                        {
                            log.Status = "Weekend Duty";
                            log.StatusClass = "k-badge-info";
                        }
                        else
                        {
                            log.Status = "Present";
                            log.StatusClass = "k-badge-approved";
                        }
                    }
                    else if (dayAtt != null && string.Equals(dayAtt.Status, "Anomaly", StringComparison.OrdinalIgnoreCase))
                    {
                        log.Status = "Anomaly (Missing Out)";
                        log.StatusClass = "k-badge-rejected";
                    }
                    else if (isWeekend)
                    {
                        log.Status = "Weekend";
                        log.StatusClass = "k-badge-secondary";
                    }
                    else if (d.Date <= now.Date)
                    {
                        log.Status = "Absent / Off";
                        log.StatusClass = "k-badge-secondary";
                    }
                    else
                    {
                        log.Status = "Upcoming";
                        log.StatusClass = "k-badge-secondary";
                    }

                    // Calculate OT for the day
                    if (isWeekend && worked > 0)
                    {
                        var ot = (decimal)Math.Floor(worked);
                        log.OtHours = ot;
                        log.OtRateLabel = $"{OtPolicy.WeekendOtMultiplier:0.#}×";
                        weekendOtHours += ot;
                        if (monthlyBaseHours > 0 && salaryMap.ContainsKey(emp.Id))
                        {
                            var hourly = salaryMap[emp.Id] / monthlyBaseHours;
                            log.OtPay = Math.Round(ot * (hourly * OtPolicy.WeekendOtMultiplier), 2);
                        }
                    }
                    else if (!isWeekend && worked > (double)OtPolicy.StandardDailyWorkingHours)
                    {
                        var ot = (decimal)Math.Floor(worked - (double)OtPolicy.StandardDailyWorkingHours);
                        log.OtHours = ot;
                        log.OtRateLabel = $"{OtPolicy.StandardOtMultiplier:0.#}×";
                        weekdayOtHours += ot;
                        if (monthlyBaseHours > 0 && salaryMap.ContainsKey(emp.Id))
                        {
                            var hourly = salaryMap[emp.Id] / monthlyBaseHours;
                            log.OtPay = Math.Round(ot * (hourly * OtPolicy.StandardOtMultiplier), 2);
                        }
                    }

                    dailyLogs.Add(log);
                }

                int paidLeaves = dailyLogs.Count(l => l.HasLeave && !l.Status.Contains("No-Pay"));
                int noPayLeaves = dailyLogs.Count(l => l.HasLeave && l.Status.Contains("No-Pay"));

                decimal totalOtHours = weekdayOtHours + weekendOtHours;

                // Calculate estimated OT pay (weekday 1.5x + weekend 2.0x)
                decimal estimatedOtPay = 0;
                decimal basicSalary = salaryMap.ContainsKey(emp.Id) ? salaryMap[emp.Id] : 0;
                if (totalOtHours > 0 && basicSalary > 0 && monthlyBaseHours > 0)
                {
                    var hourlyRate = basicSalary / monthlyBaseHours;
                    var regularOtRate = hourlyRate * OtPolicy.StandardOtMultiplier;
                    var weekendOtRate = hourlyRate * OtPolicy.WeekendOtMultiplier;

                    var regularOtPay = weekdayOtHours * regularOtRate;
                    var weekendOtPay = weekendOtHours * weekendOtRate;
                    estimatedOtPay = Math.Round(regularOtPay + weekendOtPay, 2);
                }

                string status = "Verified";
                bool hasAnomaly = empAtt.Any(a => 
                    string.Equals(a.Status, "Anomaly", StringComparison.OrdinalIgnoreCase) ||
                    (a.TimeIn.HasValue && !a.TimeOut.HasValue && a.Date.Date < now.Date));

                if (hasAnomaly)
                {
                    status = "Anomaly";
                }
                else if (!empAtt.Any() && !empLeaves.Any())
                {
                    status = "Pending";
                }

                AttendanceData.Add(new AttendanceRecord
                {
                    EmployeeId = emp.Id,
                    Name = emp.NameWithInitials,
                    EmpId = emp.EPFNumber ?? $"EMP-{emp.Id:D3}",
                    Department = emp.Department?.Name ?? "General",
                    Designation = emp.Designation?.Title ?? "Employee",
                    BasicSalary = basicSalary,
                    WorkingDays = workingDays,
                    TotalDays = totalWeekdays,
                    PaidLeaves = paidLeaves,
                    NoPayLeaves = noPayLeaves,
                    OvertimeHours = (int)totalOtHours,
                    WeekdayOtHours = weekdayOtHours,
                    WeekendOtHours = weekendOtHours,
                    EstimatedOtPay = estimatedOtPay,
                    Status = status,
                    DailyLogs = dailyLogs
                });
            }

            return Page();
        }

        public async Task<IActionResult> OnPostSyncOtToPayrollAsync(int branchId, int month, int year)
        {
            if (User.IsInRole("Admin"))
            {
                return Forbid();
            }

            // Load OT policy
            var policy = await _db.PayrollPolicySettings
                .FirstOrDefaultAsync(p => p.BranchId == branchId)
                ?? await _db.PayrollPolicySettings
                    .FirstOrDefaultAsync(p => p.BranchId == null)
                ?? new PayrollPolicySetting();

            var monthlyBaseHours = policy.StandardMonthlyWorkingDays * policy.StandardDailyWorkingHours;

            // Remove existing auto-calculated OT bonuses for this month/branch
            var existingOtBonuses = await _db.PayrollBonuses
                .Include(b => b.Employee)
                .Where(b => b.Month == month && b.Year == year && b.BonusType == "Overtime" && b.Employee != null && b.Employee.BranchId == branchId)
                .ToListAsync();
            if (existingOtBonuses.Any())
            {
                _db.PayrollBonuses.RemoveRange(existingOtBonuses);
            }

            // Fetch active employees with salaries
            var salaries = await _db.PayrollSalaries
                .Include(s => s.Employee)
                .Where(s => s.Employee != null && !s.Employee.NIC.StartsWith("DUTY") && s.Employee.NIC != "DUTY-ACC" && s.Employee.BranchId == branchId && s.Employee.Status == "Active")
                .GroupBy(s => s.EmployeeId)
                .Select(g => g.OrderByDescending(s => s.EffectiveDate).ThenByDescending(s => s.Id).First())
                .ToListAsync();

            // Fetch attendance for the month
            var startOfMonth = new DateTime(year, month, 1);
            var nextMonth = startOfMonth.AddMonths(1);
            var attendances = await _db.Attendances
                .Where(a => a.Date >= startOfMonth && a.Date < nextMonth)
                .ToListAsync();

            int otCount = 0;
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

                    _db.PayrollBonuses.Add(new PayrollBonus
                    {
                        EmployeeId = sal.EmployeeId,
                        BonusType = "Overtime",
                        Amount = totalOtPay,
                        Month = month,
                        Year = year,
                        Reason = reason
                    });
                    otCount++;
                }
            }

            await _db.SaveChangesAsync();

            var monthName = new DateTime(year, month, 1).ToString("MMMM yyyy");
            TempData["Success"] = $"OT synced to Allowances for {monthName}! {otCount} employees with overtime added.";
            return RedirectToPage(new { branchId = branchId, month = month, year = year });
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

    public class AttendanceRecord
    {
        public int EmployeeId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string EmpId { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public string Designation { get; set; } = string.Empty;
        public decimal BasicSalary { get; set; }
        public int WorkingDays { get; set; }
        public int TotalDays { get; set; }
        public int PaidLeaves { get; set; }
        public int NoPayLeaves { get; set; }
        public int OvertimeHours { get; set; }
        public decimal WeekdayOtHours { get; set; }
        public decimal WeekendOtHours { get; set; }
        public decimal EstimatedOtPay { get; set; }
        public string Status { get; set; } = "Pending";
        public List<DailyLogItem> DailyLogs { get; set; } = new();
    }

    public class DailyLogItem
    {
        public string DateStr { get; set; } = string.Empty;
        public string DayName { get; set; } = string.Empty;
        public bool IsWeekend { get; set; }
        public string TimeIn { get; set; } = "—";
        public string TimeOut { get; set; } = "—";
        public double TotalHours { get; set; }
        public string Status { get; set; } = string.Empty;
        public string StatusClass { get; set; } = "k-badge-secondary";
        public bool HasLeave { get; set; }
        public string LeaveType { get; set; } = string.Empty;
        public decimal OtHours { get; set; }
        public string OtRateLabel { get; set; } = string.Empty;
        public decimal OtPay { get; set; }
    }
}
