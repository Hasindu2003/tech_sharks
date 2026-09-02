using HRMS.Domain.Entities.Core;
using HRMS.Domain.Common;
using HRMS.Infrastructure.Persistence;
using HRMS.UI.Pages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HRMS.UI.Pages.Performance
{
    [Authorize(Roles = "HR Manager,HR Officer,Area Manager,Branch Manager,Department Head,Employee")]
    public class IndexModel : BasePageModel
    {
        public IndexModel(ApplicationDbContext context) : base(context) { }

        public int TotalEmployees { get; set; }
        public double AvgPerformanceScore { get; set; }
        public int TopPerformerCount { get; set; }
        public double AvgAttendanceRate { get; set; }
        public string ScopedBranchNames { get; set; } = "";

        [BindProperty(SupportsGet = true)]
        public int? FilterBranchId { get; set; }

        public bool ShowBranchFilter { get; set; }
        public string BranchFilterPlaceholder { get; set; } = "-- All Branches --";
        public List<Branch> ManagedBranchesList { get; set; } = new();

        public List<BranchPerformance> BranchStats { get; set; } = new();
        public List<DepartmentPerformance> DepartmentStats { get; set; } = new();
        public List<EmployeePerformance> AllEmployees { get; set; } = new();
        public string EvaluationPeriodText { get; set; } = "";

        public async Task<IActionResult> OnGetAsync()
        {
            if (User.IsInRole("Admin"))
            {
                return Forbid();
            }

            await LoadCurrentUserAsync();

            var username = User.Identity?.Name;
            var userAccount = await _db.Users.FirstOrDefaultAsync(u => u.UserName == username || u.Email == username);
            if (userAccount == null) return Challenge();

            var employeeRecord = await _db.Employees.FirstOrDefaultAsync(e => e.Email == userAccount.Email);

            ShowBranchFilter = User.IsInRole("HR Manager") || 
                               User.IsInRole("HR Officer") || 
                               User.IsInRole("Area Manager");

            var employeesQuery = _db.Employees
                .Include(e => e.Department)
                .Include(e => e.Designation)
                .Include(e => e.Branch)
                .Where(e => !e.NIC.StartsWith("DUTY") && e.NIC != "DUTY-ACC" && e.Status != "Draft" &&
                            (e.Department == null || 
                             (!e.Department.Name.Equals("Managerial") && 
                              !e.Department.Name.Equals("Management") && 
                              !e.Department.Name.Contains("Managerial") && 
                              !e.Department.Name.Contains("Management"))));

            if (User.IsInRole("HR Manager"))
            {
                BranchFilterPlaceholder = "-- All Branches --";
                ManagedBranchesList = await _db.Branches.OrderBy(b => b.Name).ToListAsync();

                if (FilterBranchId.HasValue && FilterBranchId.Value > 0)
                {
                    employeesQuery = employeesQuery.Where(e => e.BranchId == FilterBranchId.Value);
                    var selBranch = ManagedBranchesList.FirstOrDefault(b => b.Id == FilterBranchId.Value);
                    if (selBranch != null) ScopedBranchNames = selBranch.Name;
                }
            }
            else if (User.IsInRole("Area Manager") || User.IsInRole("HR Officer"))
            {
                BranchFilterPlaceholder = User.IsInRole("Area Manager") ? "-- All Assigned Branches --" : "-- All Assigned Branches --";
                var managedStr = userAccount?.ManagedBranches ?? "";
                var assignedBranchIds = managedStr
                    .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(s => int.TryParse(s, out var id) ? id : 0)
                    .Where(id => id > 0)
                    .ToList();

                if (!assignedBranchIds.Any())
                {
                    if (!string.IsNullOrWhiteSpace(userAccount?.Branch))
                    {
                        var branch = await _db.Branches.FirstOrDefaultAsync(b => b.Name.ToLower() == userAccount.Branch.Trim().ToLower());
                        if (branch != null) assignedBranchIds.Add(branch.Id);
                    }
                    if (!assignedBranchIds.Any()) assignedBranchIds.Add(-1);
                }

                ManagedBranchesList = await _db.Branches
                    .Where(b => assignedBranchIds.Contains(b.Id))
                    .OrderBy(b => b.Name)
                    .ToListAsync();

                if (FilterBranchId.HasValue && FilterBranchId.Value > 0 && assignedBranchIds.Contains(FilterBranchId.Value))
                {
                    employeesQuery = employeesQuery.Where(e => e.BranchId == FilterBranchId.Value);
                    var selBranch = ManagedBranchesList.FirstOrDefault(b => b.Id == FilterBranchId.Value);
                    if (selBranch != null) ScopedBranchNames = selBranch.Name;
                }
                else
                {
                    employeesQuery = employeesQuery.Where(e => assignedBranchIds.Contains(e.BranchId));
                    var assignedBranchNames = ManagedBranchesList.Select(b => b.Name).ToList();
                    if (assignedBranchNames.Any())
                    {
                        ScopedBranchNames = string.Join(", ", assignedBranchNames);
                    }
                }
            }
            else
            {
                int scopedBranchId = -1;
                if (employeeRecord?.BranchId > 0)
                {
                    scopedBranchId = employeeRecord.BranchId;
                }
                else if (!string.IsNullOrWhiteSpace(userAccount?.Branch))
                {
                    var branch = await _db.Branches.FirstOrDefaultAsync(b => b.Name.ToLower() == userAccount.Branch.Trim().ToLower());
                    scopedBranchId = branch?.Id ?? -1;
                }

                employeesQuery = employeesQuery.Where(e => e.BranchId == scopedBranchId);
                var branchObj = await _db.Branches.FindAsync(scopedBranchId);
                if (branchObj != null) ScopedBranchNames = branchObj.Name;
            }

            var allEmployees = await employeesQuery.ToListAsync();

            TotalEmployees = allEmployees.Count;

            var today = SriLankaTime.Today;
            var cutoff = today.AddDays(-30).Date;
            var todayEnd = today.AddDays(1);
            var year = today.Year;

            EvaluationPeriodText = $"Last 30 Days ({cutoff:MMM dd, yyyy} – {today:MMM dd, yyyy})";

            int elapsedBusinessDays = 0;
            for (var d = cutoff; d <= today; d = d.AddDays(1))
            {
                if (d.DayOfWeek != DayOfWeek.Saturday && d.DayOfWeek != DayOfWeek.Sunday)
                {
                    elapsedBusinessDays++;
                }
            }

            var allAttendance = await _db.Attendances
                .Where(a => a.Date >= cutoff && a.Date < todayEnd)
                .ToListAsync();

            var companyWorkingDates = allAttendance
                .Select(a => a.Date.Date)
                .Distinct()
                .OrderBy(d => d)
                .ToList();

            int totalCompanyWorkingDates = companyWorkingDates.Count;
            int companyBenchmarkDays = Math.Max(1, Math.Max(totalCompanyWorkingDates, elapsedBusinessDays));

            var allEntitlements = await _db.LeaveEntitlements
                .Where(e => e.Year == year && (e.LeaveType == "Annual" || e.LeaveType == "Casual" || e.LeaveType.Contains("Annual") || e.LeaveType.Contains("Casual")))
                .ToListAsync();

            var allApprovedLeaves = await _db.Leaves
                .Where(l => l.Status == "Approved" && l.EndDate >= cutoff && l.StartDate <= today
                         && (l.LeaveType == "Annual" || l.LeaveType == "Casual" || l.LeaveType.Contains("Annual") || l.LeaveType.Contains("Casual")))
                .ToListAsync();

            var scores = new List<EmployeePerformance>();

            foreach (var emp in allEmployees)
            {
                // Attendance (45%) & Punctuality (25%)
                var empAtt = allAttendance.Where(a => a.EmployeeId == emp.Id).ToList();
                double attendanceScore = 0.0;
                double punctualityScore = 0.0;
                int totalDays = empAtt.Count;
                int presentDays = 0;
                int lateDays = 0;
                int halfDays = 0;
                int onLeaveDays = 0;
                int absentDays = 0;
                int onTimeDays = 0;

                if (empAtt.Any())
                {
                    foreach (var a in empAtt)
                    {
                        var status = (a.Status ?? "").Trim();
                        bool isLate = false;

                        // Check for late arrival (marked "Late" or clocked in after 08:30 AM standard shift start)
                        if (string.Equals(status, "Late", StringComparison.OrdinalIgnoreCase))
                        {
                            isLate = true;
                        }
                        else if (a.TimeIn.HasValue && a.TimeIn.Value.TimeOfDay > new TimeSpan(8, 30, 0))
                        {
                            isLate = true;
                        }

                        if (string.Equals(status, "Present", StringComparison.OrdinalIgnoreCase))
                        {
                            presentDays++;
                            if (isLate) lateDays++;
                            else onTimeDays++;
                        }
                        else if (string.Equals(status, "Late", StringComparison.OrdinalIgnoreCase))
                        {
                            presentDays++;
                            lateDays++;
                        }
                        else if (string.Equals(status, "Half Day", StringComparison.OrdinalIgnoreCase) ||
                                 string.Equals(status, "HalfDay", StringComparison.OrdinalIgnoreCase))
                        {
                            halfDays++;
                            if (isLate) lateDays++;
                            else onTimeDays++;
                        }
                        else if (string.Equals(status, "OnLeave", StringComparison.OrdinalIgnoreCase) ||
                                 string.Equals(status, "On Leave", StringComparison.OrdinalIgnoreCase) ||
                                 string.Equals(status, "Leave", StringComparison.OrdinalIgnoreCase))
                        {
                            onLeaveDays++;
                        }
                        else if (string.Equals(status, "Absent", StringComparison.OrdinalIgnoreCase))
                        {
                            absentDays++;
                        }
                        else
                        {
                            if (a.TimeIn.HasValue && a.TimeIn.Value.TimeOfDay > TimeSpan.Zero)
                            {
                                presentDays++;
                                if (isLate) lateDays++;
                                else onTimeDays++;
                            }
                            else
                            {
                                absentDays++;
                            }
                        }
                    }

                    // Attendance Score (45%) evaluated against fixed corporate monthly target of 20 business days
                    const int standardBenchmarkWorkingDays = 20;
                    double attendedEquiv = presentDays + (halfDays * 0.5);
                    attendanceScore = Math.Min(100.0, Math.Round((attendedEquiv / standardBenchmarkWorkingDays) * 100.0, 1));

                    // Punctuality Score (25%) evaluated strictly on days actually attended
                    int totalAttended = presentDays + halfDays;
                    if (totalAttended > 0)
                    {
                        punctualityScore = Math.Round(((double)onTimeDays / totalAttended) * 100.0, 1);
                    }
                    else
                    {
                        punctualityScore = 0.0;
                    }
                }
                else
                {
                    // No attendance records logged for this employee in evaluation window
                    attendanceScore = 0.0;
                    punctualityScore = 0.0;
                }

                attendanceScore = Math.Max(0, Math.Min(100, attendanceScore));
                punctualityScore = Math.Max(0, Math.Min(100, punctualityScore));

                // Leave Discipline score (30%) - Evaluated ONLY on Annual and Casual leaves taken in last 30 days against 20 business days
                var empLeavesIn30d = allApprovedLeaves
                    .Where(l => l.EmployeeId == emp.Id && l.EndDate >= cutoff && l.StartDate <= today
                             && (l.LeaveType.Equals("Annual", StringComparison.OrdinalIgnoreCase) || 
                                 l.LeaveType.Equals("Casual", StringComparison.OrdinalIgnoreCase) || 
                                 l.LeaveType.Contains("Annual") || 
                                 l.LeaveType.Contains("Casual")))
                    .ToList();

                int leaveDaysUsedIn30d = 0;
                foreach (var l in empLeavesIn30d)
                {
                    var s = l.StartDate < cutoff ? cutoff : l.StartDate;
                    var e = l.EndDate > today ? today : l.EndDate;
                    for (var d = s.Date; d <= e.Date; d = d.AddDays(1))
                    {
                        if (d.DayOfWeek != DayOfWeek.Saturday && d.DayOfWeek != DayOfWeek.Sunday)
                        {
                            leaveDaysUsedIn30d++;
                        }
                    }
                }

                int leaveDaysUsed = leaveDaysUsedIn30d;
                int leaveDaysTotal = 20;

                double leaveScore = Math.Max(0.0, Math.Min(100.0, Math.Round(((double)(leaveDaysTotal - leaveDaysUsed) / leaveDaysTotal) * 100.0, 1)));

                // Weighted final score: Attendance (45%) + Leave Discipline (30%) + Punctuality (25%) = 100%
                double finalScore = Math.Max(0, Math.Min(100,
                    (attendanceScore * 0.45) +
                    (leaveScore * 0.30) +
                    (punctualityScore * 0.25)));

                finalScore = Math.Round(finalScore, 1);

                string grade = finalScore switch
                {
                    >= 90 => "A+",
                    >= 80 => "A",
                    >= 70 => "B",
                    >= 60 => "C",
                    _ => "D"
                };

                scores.Add(new EmployeePerformance
                {
                    Employee = emp,
                    AttendanceScore = attendanceScore,
                    PunctualityScore = punctualityScore,
                    LeaveScore = leaveScore,
                    PerformanceScore = finalScore,
                    Grade = grade,
                    PresentDays = presentDays,
                    TotalDays = totalDays,
                    LateDays = lateDays,
                    OnTimeDays = onTimeDays,
                    LeaveDaysUsed = leaveDaysUsed,
                    LeaveDaysTotal = leaveDaysTotal,
                    Status = emp.Status
                });
            }

            scores = scores.OrderByDescending(e => e.PerformanceScore).ToList();
            for (int i = 0; i < scores.Count; i++) scores[i].Rank = i + 1;

            AllEmployees = scores;
            TopPerformerCount = scores.Count(e => e.PerformanceScore >= 80);

            AvgPerformanceScore = scores.Any()
                ? Math.Round(scores.Average(e => e.PerformanceScore), 1) : 0;

            AvgAttendanceRate = scores.Any()
                ? Math.Round(scores.Average(e => e.AttendanceScore), 1) : 0;

            var deptGroups = scores
                .Where(e => e.Employee.Department != null && 
                            !e.Employee.Department.Name.Equals("Managerial", StringComparison.OrdinalIgnoreCase) &&
                            !e.Employee.Department.Name.Equals("Management", StringComparison.OrdinalIgnoreCase) &&
                            !e.Employee.Department.Name.Contains("Managerial", StringComparison.OrdinalIgnoreCase) &&
                            !e.Employee.Department.Name.Contains("Management", StringComparison.OrdinalIgnoreCase))
                .GroupBy(e => e.Employee.Department!.Name)
                .ToList();

            double maxScore = deptGroups.Any()
                ? deptGroups.Max(g => g.Average(e => e.PerformanceScore)) : 100;

            DepartmentStats = deptGroups.Select(g => new DepartmentPerformance
            {
                DepartmentName = g.Key,
                AvgScore = Math.Round(g.Average(e => e.PerformanceScore), 1),
                EmployeeCount = g.Count(),
                BarHeightPercent = maxScore > 0
                    ? (int)Math.Round(g.Average(e => e.PerformanceScore) / maxScore * 100)
                    : 0
            }).OrderByDescending(d => d.AvgScore).Take(6).ToList();

            var branchGroups = scores
                .Where(e => e.Employee.Branch != null)
                .GroupBy(e => new { e.Employee.BranchId, e.Employee.Branch!.Name })
                .ToList();

            double maxBranchScore = branchGroups.Any()
                ? branchGroups.Max(g => g.Average(e => e.PerformanceScore)) : 100;

            BranchStats = branchGroups.Select(g => new BranchPerformance
            {
                BranchId = g.Key.BranchId,
                BranchName = g.Key.Name,
                AvgScore = Math.Round(g.Average(e => e.PerformanceScore), 1),
                EmployeeCount = g.Count(),
                TopPerformers = g.Count(e => e.PerformanceScore >= 80),
                AvgAttendanceRate = Math.Round(g.Average(e => e.AttendanceScore), 1),
                BarHeightPercent = maxBranchScore > 0
                    ? (int)Math.Round(g.Average(e => e.PerformanceScore) / maxBranchScore * 100)
                    : 0
            }).OrderByDescending(b => b.AvgScore).ToList();

            return Page();
        }
    }

    public class EmployeePerformance
    {
        public HRMS.Domain.Entities.Core.Employee Employee { get; set; } = null!;
        public string NameWithInitials => FormatNameWithInitials(Employee?.FullName, Employee?.Initials);
        public int Rank { get; set; }
        public double AttendanceScore { get; set; }
        public double PunctualityScore { get; set; }
        public double LeaveScore { get; set; }
        public double TrainingScore { get; set; }
        public double PerformanceScore { get; set; }
        public string Grade { get; set; } = "C";
        public string Status { get; set; } = "Active";
        public int PresentDays { get; set; }
        public int TotalDays { get; set; }
        public int LateDays { get; set; }
        public int OnTimeDays { get; set; }
        public int LeaveDaysUsed { get; set; }
        public int LeaveDaysTotal { get; set; }

        public static string FormatNameWithInitials(string? fullName, string? initials)
        {
            if (!string.IsNullOrWhiteSpace(initials))
            {
                return initials.Trim();
            }

            if (!string.IsNullOrWhiteSpace(fullName))
            {
                var parts = fullName.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 1)
                {
                    var initPart = string.Join(" ", parts.Take(parts.Length - 1).Select(p => p[0].ToString().ToUpper() + "."));
                    var lastName = parts[^1];
                    return $"{initPart} {lastName}";
                }
                return parts[0];
            }

            return "Unknown";
        }

        public string GradeBadgeColor => Grade switch
        {
            "A+" => "k-badge-approved",
            "A" => "k-badge-approved",
            "B" => "k-badge-info",
            "C" => "k-badge-pending",
            _ => "k-badge-rejected"
        };

        public string RankBadgeColor => Rank switch
        {
            1 => "background: #fef3c7; color: #b45309;",
            2 => "background: #f3f4f6; color: #4b5563;",
            3 => "background: #ffedd5; color: #c2410c;",
            _ => "background: var(--bg-body); color: var(--text-secondary);"
        };
    }

    public class DepartmentPerformance
    {
        public string DepartmentName { get; set; } = "";
        public double AvgScore { get; set; }
        public int EmployeeCount { get; set; }
        public int BarHeightPercent { get; set; }
    }

    public class BranchPerformance
    {
        public int BranchId { get; set; }
        public string BranchName { get; set; } = "";
        public double AvgScore { get; set; }
        public int EmployeeCount { get; set; }
        public int TopPerformers { get; set; }
        public double AvgAttendanceRate { get; set; }
        public int BarHeightPercent { get; set; }
    }
}
