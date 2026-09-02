using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using HRMS.Infrastructure.Identity;
using HRMS.Infrastructure.Persistence;
using HRMS.Application.Services;
using HRMS.UI.Services;
using CoreEmployee = HRMS.Domain.Entities.Core.Employee;
using HRMS.Domain.Entities.Attendance;
using HRMS.Domain.Entities.Leave;
using HRMS.Domain.Entities.Payroll;
using HRMS.Domain.Entities.Transfer;
using HRMS.Domain.Entities.Welfare;
using HRMS.Domain.Entities.Calendar;
using HRMS.Domain.Common;

namespace HRMS.UI.Pages
{
    [Authorize]
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ITransferRequestService _transferService;
        private readonly ILeaveService _leaveService;

        public IndexModel(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            ITransferRequestService transferService,
            ILeaveService leaveService)
        {
            _context = context;
            _userManager = userManager;
            _transferService = transferService;
            _leaveService = leaveService;
        }

        // View Mode
        public bool IsEmployeeView { get; set; }

        // Common Greeting & Profile
        public string GreetingName { get; set; } = "User";
        public string GreetingTime { get; set; } = "Good Morning";
        public string UserRoleDisplay { get; set; } = "Staff Member";
        public EmployeeProfileInfo? EmployeeInfo { get; set; }

        // ==================== EMPLOYEE DASHBOARD DATA ====================
        // Attendance & OT Summary
        public int DaysWorkedThisMonth { get; set; }
        public int TotalWorkingDaysThisMonth { get; set; } = 21;
        public int AttendanceRatePercent { get; set; }
        public double MonthlyWorkedHours { get; set; }
        public decimal MonthlyOvertimeHours { get; set; }
        public decimal MonthlyEstimatedOtPay { get; set; }

        // Today's Check-in / Check-out
        public string TodayCheckInTime { get; set; } = "—";
        public string TodayCheckOutTime { get; set; } = "—";
        public double TodayHoursWorked { get; set; }
        public string ShiftTiming { get; set; } = "08:00 AM – 04:00 PM";

        // Leave Summary
        public double AvailableLeaveBalance { get; set; }
        public double AnnualLeavesRemaining { get; set; }
        public double CasualLeavesRemaining { get; set; }
        public double MedicalLeavesRemaining { get; set; }
        public double LeavesTakenThisMonth { get; set; }
        public int PendingLeavesCount { get; set; }

        // Latest Payslip Snapshot
        public bool HasPayslip { get; set; }
        public string LatestPayslipMonth { get; set; } = "";
        public decimal LatestNetPay { get; set; }
        public string LatestPayslipStatus { get; set; } = "Ready";
        public int? LatestPayslipId { get; set; }

        // Unified Recent Self-Service Requests
        public List<EmployeeRequestItem> RecentRequests { get; set; } = new();

        // Recent Attendance History (Last 5 Days)
        public List<EmployeeAttendanceLogItem> RecentAttendanceLogs { get; set; } = new();

        // ==================== MANAGER DASHBOARD DATA ====================
        public int TotalEmployees { get; set; }
        public int OnLeaveToday { get; set; }
        public int PendingRequests { get; set; }
        public int OpenPositions { get; set; }
        public List<PendingApprovalItem> PendingApprovals { get; set; } = new();

        // Upcoming Events (Common)
        public List<UpcomingEventItem> UpcomingEvents { get; set; } = new();

        public async Task<IActionResult> OnGetAsync()
        {
            if (User.IsInRole("Welfare Manager"))
            {
                return RedirectToPage("/Welfare/Approvals/DepartmentHeadApproval");
            }

            try
            {
                var currentUser = await _userManager.GetUserAsync(User);
                var email = currentUser?.Email ?? User.Identity?.Name ?? "";
                var userName = currentUser?.UserName ?? User.Identity?.Name ?? "";
                var userEpf = currentUser?.EpfNumber ?? "";

                // Determine if this is a regular employee dashboard
                bool isManagerOrAdmin = User.IsInRole("Admin") ||
                                       User.IsInRole("HR Manager") ||
                                       User.IsInRole("HR Officer") ||
                                       User.IsInRole("Area Manager") ||
                                       User.IsInRole("Branch Manager") ||
                                       User.IsInRole("Department Head") ||
                                       User.IsInRole("Welfare Manager");

                IsEmployeeView = User.IsInRole("Employee") && !isManagerOrAdmin;

                // Greeting Time
                var now = SriLankaTime.Now;
                var hour = now.Hour;
                GreetingTime = hour < 12 ? "Good Morning" : hour < 17 ? "Good Afternoon" : "Good Evening";

                // Load Employee Entity with robust fallbacks
                CoreEmployee? employee = null;

                if (currentUser?.EmployeeId.HasValue == true)
                {
                    employee = await _context.Employees
                        .Include(e => e.Department)
                        .Include(e => e.Designation)
                        .Include(e => e.Branch)
                        .FirstOrDefaultAsync(e => e.Id == currentUser.EmployeeId.Value);
                }

                if (employee == null && !string.IsNullOrEmpty(email))
                {
                    employee = await _context.Employees
                        .Include(e => e.Department)
                        .Include(e => e.Designation)
                        .Include(e => e.Branch)
                        .FirstOrDefaultAsync(e => e.Email == email);
                }

                if (employee == null && !string.IsNullOrEmpty(userEpf))
                {
                    employee = await _context.Employees
                        .Include(e => e.Department)
                        .Include(e => e.Designation)
                        .Include(e => e.Branch)
                        .FirstOrDefaultAsync(e => e.EPFNumber == userEpf);
                }

                if (employee == null && !string.IsNullOrEmpty(userName))
                {
                    employee = await _context.Employees
                        .Include(e => e.Department)
                        .Include(e => e.Designation)
                        .Include(e => e.Branch)
                        .FirstOrDefaultAsync(e => e.NIC == userName || e.Email == userName || e.EPFNumber == userName);
                }

                // Fallback for employee view if user account is not linked to an employee row yet
                if (employee == null && IsEmployeeView)
                {
                    employee = await _context.Employees
                        .Include(e => e.Department)
                        .Include(e => e.Designation)
                        .Include(e => e.Branch)
                        .FirstOrDefaultAsync(e => !e.NIC.StartsWith("DUTY") && e.Status != "Terminated" && e.Status != "Draft");
                }

                GreetingName = employee?.FullName ?? currentUser?.FullName ?? userName ?? "Employee";
                if (!string.IsNullOrWhiteSpace(GreetingName) && GreetingName.Contains(" "))
                {
                    GreetingName = GreetingName.Split(' ')[0];
                }

                if (employee != null)
                {
                    EmployeeInfo = new EmployeeProfileInfo
                    {
                        Id = employee.Id,
                        FullName = employee.FullName,
                        NameWithInitials = employee.NameWithInitials,
                        EmpCode = employee.EPFNumber ?? $"EMP-{employee.Id:D3}",
                        DepartmentName = employee.Department?.Name ?? "General",
                        DesignationTitle = employee.Designation?.Title ?? "Staff Member",
                        BranchName = employee.Branch?.Name ?? "Main Branch",
                        AvatarInitial = string.IsNullOrEmpty(employee.FullName) ? "E" : employee.FullName.Substring(0, 1).ToUpper()
                    };
                    UserRoleDisplay = EmployeeInfo.DesignationTitle;
                }
                else
                {
                    UserRoleDisplay = isManagerOrAdmin ? "Management" : "Employee";
                }

                if (IsEmployeeView && employee != null)
                {
                    // =========================================================================
                    // LOAD EMPLOYEE-SPECIFIC METRICS
                    // =========================================================================
                    var today = now.Date;
                    var startOfMonth = new DateTime(now.Year, now.Month, 1);
                    var nextMonth = startOfMonth.AddMonths(1);

                    // 1. Attendance & Working Days This Month
                    var monthAtt = await _context.Attendances
                        .Where(a => a.EmployeeId == employee.Id && a.Date >= startOfMonth && a.Date < nextMonth)
                        .ToListAsync();

                    DaysWorkedThisMonth = monthAtt
                        .Where(a => string.Equals(a.Status, "Present", StringComparison.OrdinalIgnoreCase) ||
                                    string.Equals(a.Status, "Late", StringComparison.OrdinalIgnoreCase) ||
                                    string.Equals(a.Status, "Verified", StringComparison.OrdinalIgnoreCase) ||
                                    (a.TotalHours.HasValue && a.TotalHours.Value > 0) ||
                                    (a.TimeIn.HasValue && a.TimeOut.HasValue))
                        .Select(a => a.Date.Date)
                        .Distinct()
                        .Count();

                    int standardWorkingDays = 21;
                    TotalWorkingDaysThisMonth = standardWorkingDays;
                    AttendanceRatePercent = TotalWorkingDaysThisMonth > 0 ? (int)Math.Min(100, Math.Round((double)DaysWorkedThisMonth / TotalWorkingDaysThisMonth * 100)) : 100;

                    MonthlyWorkedHours = Math.Round(monthAtt.Sum(a => a.TotalHours ?? (a.TimeIn.HasValue && a.TimeOut.HasValue ? (a.TimeOut.Value - a.TimeIn.Value).TotalHours : 0)), 1);

                    // Overtime calculation (dual rate 1.5x / 2.0x)
                    var otPolicy = await _context.PayrollPolicySettings
                        .FirstOrDefaultAsync(p => p.BranchId == employee.BranchId) 
                        ?? await _context.PayrollPolicySettings.FirstOrDefaultAsync(p => p.BranchId == null)
                        ?? new PayrollPolicySetting();

                    decimal weekdayOt = 0;
                    decimal weekendOt = 0;
                    foreach (var a in monthAtt)
                    {
                        double worked = a.TotalHours ?? (a.TimeIn.HasValue && a.TimeOut.HasValue ? (a.TimeOut.Value - a.TimeIn.Value).TotalHours : 0);
                        bool isWk = a.Date.DayOfWeek == DayOfWeek.Saturday || a.Date.DayOfWeek == DayOfWeek.Sunday;
                        if (isWk && worked > 0)
                        {
                            weekendOt += (decimal)Math.Floor(worked);
                        }
                        else if (!isWk && worked > (double)otPolicy.StandardDailyWorkingHours)
                        {
                            weekdayOt += (decimal)Math.Floor(worked - (double)otPolicy.StandardDailyWorkingHours);
                        }
                    }
                    MonthlyOvertimeHours = weekdayOt + weekendOt;

                    // Salary for OT calculation
                    var salaryRecord = await _context.PayrollSalaries.FirstOrDefaultAsync(s => s.EmployeeId == employee.Id);
                    decimal basicSalary = salaryRecord?.BasicSalary ?? 0;
                    decimal monthlyBaseHours = otPolicy.StandardMonthlyWorkingDays * otPolicy.StandardDailyWorkingHours;
                    if (monthlyBaseHours > 0 && basicSalary > 0 && MonthlyOvertimeHours > 0)
                    {
                        decimal hourly = basicSalary / monthlyBaseHours;
                        MonthlyEstimatedOtPay = Math.Round((weekdayOt * hourly * otPolicy.StandardOtMultiplier) + (weekendOt * hourly * otPolicy.WeekendOtMultiplier), 2);
                    }

                    // 2. Today's Attendance Punch Timings
                    var todayAtt = monthAtt.FirstOrDefault(a => a.Date.Date == today);
                    if (todayAtt != null)
                    {
                        TodayCheckInTime = todayAtt.TimeIn?.ToString("hh:mm tt") ?? "—";
                        TodayCheckOutTime = todayAtt.TimeOut?.ToString("hh:mm tt") ?? "—";
                        TodayHoursWorked = todayAtt.TotalHours ?? (todayAtt.TimeIn.HasValue && todayAtt.TimeOut.HasValue ? Math.Round((todayAtt.TimeOut.Value - todayAtt.TimeIn.Value).TotalHours, 1) : 0);
                    }

                    // 3. Leave Balances from LeaveService
                    try
                    {
                        var balances = await _leaveService.GetAllLeaveBalancesAsync(employee.Id, now.Year);
                        AnnualLeavesRemaining = balances.FirstOrDefault(b => b.LeaveType.Equals("Annual", StringComparison.OrdinalIgnoreCase))?.RemainingDays ?? 0;
                        CasualLeavesRemaining = balances.FirstOrDefault(b => b.LeaveType.Equals("Casual", StringComparison.OrdinalIgnoreCase))?.RemainingDays ?? 0;
                        MedicalLeavesRemaining = balances.FirstOrDefault(b => b.LeaveType.Equals("Medical", StringComparison.OrdinalIgnoreCase))?.RemainingDays ?? 0;
                        
                        AvailableLeaveBalance = AnnualLeavesRemaining + CasualLeavesRemaining + MedicalLeavesRemaining;

                        if (AvailableLeaveBalance == 0 && balances.Any())
                        {
                            AvailableLeaveBalance = balances.Where(b => !b.LeaveType.Equals("Maternity", StringComparison.OrdinalIgnoreCase)).Sum(b => b.RemainingDays);
                        }
                    }
                    catch
                    {
                        AvailableLeaveBalance = 0;
                    }

                    // Fallback to entitlement calculation if not seeded yet
                    if (AvailableLeaveBalance == 0)
                    {
                        double usedAnnual = await _context.Leaves.Where(l => l.EmployeeId == employee.Id && l.Status == "Approved" && l.LeaveType.Contains("Annual") && l.StartDate.Year == now.Year).SumAsync(l => l.TotalDays);
                        double usedCasual = await _context.Leaves.Where(l => l.EmployeeId == employee.Id && l.Status == "Approved" && l.LeaveType.Contains("Casual") && l.StartDate.Year == now.Year).SumAsync(l => l.TotalDays);
                        double usedMedical = await _context.Leaves.Where(l => l.EmployeeId == employee.Id && l.Status == "Approved" && l.LeaveType.Contains("Medical") && l.StartDate.Year == now.Year).SumAsync(l => l.TotalDays);

                        AnnualLeavesRemaining = Math.Max(0, 14.0 - usedAnnual);
                        CasualLeavesRemaining = Math.Max(0, 7.0 - usedCasual);
                        MedicalLeavesRemaining = Math.Max(0, 7.0 - usedMedical);
                        AvailableLeaveBalance = AnnualLeavesRemaining + CasualLeavesRemaining + MedicalLeavesRemaining;
                    }

                    PendingLeavesCount = await _context.Leaves
                        .CountAsync(l => l.EmployeeId == employee.Id && l.Status != null && l.Status.StartsWith("Pending"));

                    LeavesTakenThisMonth = await _context.Leaves
                        .Where(l => l.EmployeeId == employee.Id && l.Status == "Approved" && l.StartDate < nextMonth && l.EndDate >= startOfMonth)
                        .SumAsync(l => l.TotalDays);

                    // 4. Latest Payslip Snapshot
                    var latestPayslip = await _context.Payslips
                        .Include(p => p.PayrollRun)
                        .Where(p => p.EmployeeId == employee.Id)
                        .OrderByDescending(p => p.PayrollRun != null ? p.PayrollRun.Year : 0)
                        .ThenByDescending(p => p.PayrollRun != null ? p.PayrollRun.Month : 0)
                        .FirstOrDefaultAsync();

                    if (latestPayslip != null && latestPayslip.PayrollRun != null)
                    {
                        HasPayslip = true;
                        LatestPayslipMonth = new DateTime(latestPayslip.PayrollRun.Year, latestPayslip.PayrollRun.Month, 1).ToString("MMMM yyyy");
                        LatestNetPay = latestPayslip.NetPay;
                        LatestPayslipStatus = latestPayslip.Status;
                        LatestPayslipId = latestPayslip.Id;
                    }

                    // 5. Recent Self-Service Requests (Unified Feed)
                    var myLeaves = await _context.Leaves
                        .Where(l => l.EmployeeId == employee.Id)
                        .OrderByDescending(l => l.AppliedDate)
                        .Take(4)
                        .ToListAsync();

                    foreach (var l in myLeaves)
                    {
                        RecentRequests.Add(new EmployeeRequestItem
                        {
                            Category = "Leave Application",
                            Title = $"{l.LeaveType} Leave ({l.TotalDays} {(l.TotalDays == 1 ? "day" : "days")})",
                            SubmittedDate = l.AppliedDate.ToString("MMM dd, yyyy"),
                            DateRange = $"{l.StartDate:MMM dd} - {l.EndDate:MMM dd}",
                            Status = l.Status,
                            BadgeClass = l.Status == "Approved" ? "st-approved" : l.Status == "Rejected" ? "st-rejected" : "st-pending",
                            ActionUrl = "/Employee/Leave/Dashboard"
                        });
                    }

                    var myTransfers = await _transferService.GetRequestsByUserAsync(email);
                    foreach (var t in myTransfers.Take(2))
                    {
                        string statusStr = t.Status.ToString();
                        RecentRequests.Add(new EmployeeRequestItem
                        {
                            Category = "Branch Transfer",
                            Title = $"Transfer to {t.RequestedBranch}",
                            SubmittedDate = t.RequestedDate.ToString("MMM dd, yyyy"),
                            DateRange = t.RequestedDate.ToString("MMM dd, yyyy"),
                            Status = statusStr,
                            BadgeClass = statusStr == "Approved" ? "st-approved" : statusStr == "Rejected" ? "st-rejected" : "st-pending",
                            ActionUrl = $"/Transfer/Details/{t.Id}"
                        });
                    }

                    var myWelfare = await _context.WelfareRequests
                        .Include(w => w.WelfareType)
                        .Where(w => w.EmployeeId == employee.Id)
                        .OrderByDescending(w => w.CreatedAt)
                        .Take(2)
                        .ToListAsync();

                    foreach (var w in myWelfare)
                    {
                        RecentRequests.Add(new EmployeeRequestItem
                        {
                            Category = "Welfare & Benefit",
                            Title = $"{w.WelfareType?.TypeName ?? "Welfare"} Request",
                            SubmittedDate = w.CreatedAt.ToString("MMM dd, yyyy"),
                            DateRange = w.CreatedAt.ToString("MMM dd, yyyy"),
                            Status = w.Status,
                            BadgeClass = w.Status == "Approved" ? "st-approved" : w.Status == "Rejected" ? "st-rejected" : "st-pending",
                            ActionUrl = "/Welfare/StatusTracking"
                        });
                    }

                    RecentRequests = RecentRequests
                        .OrderByDescending(r => DateTime.TryParse(r.SubmittedDate, out var dt) ? dt : DateTime.MinValue)
                        .Take(5)
                        .ToList();

                    // 6. Recent Attendance History (Last 5 Days)
                    var recentFiveDays = new List<EmployeeAttendanceLogItem>();
                    for (int i = 0; i < 7 && recentFiveDays.Count < 5; i++)
                    {
                        var checkDate = today.AddDays(-i);
                        bool isWk = checkDate.DayOfWeek == DayOfWeek.Saturday || checkDate.DayOfWeek == DayOfWeek.Sunday;
                        if (isWk && i > 0) continue; // skip past weekends unless worked

                        var log = monthAtt.FirstOrDefault(a => a.Date.Date == checkDate);
                        var lRec = await _context.Leaves.FirstOrDefaultAsync(l => l.EmployeeId == employee.Id && l.Status == "Approved" && l.StartDate.Date <= checkDate && l.EndDate.Date >= checkDate);

                        string dayStatus = "Absent / Off";
                        string statusClass = "k-badge-secondary";
                        string timeIn = "—";
                        string timeOut = "—";
                        double hrs = 0;

                        if (lRec != null)
                        {
                            dayStatus = $"Leave ({lRec.LeaveType})";
                            statusClass = "k-badge-warning";
                        }
                        else if (log != null)
                        {
                            timeIn = log.TimeIn?.ToString("hh:mm tt") ?? "—";
                            timeOut = log.TimeOut?.ToString("hh:mm tt") ?? "—";
                            hrs = log.TotalHours ?? (log.TimeIn.HasValue && log.TimeOut.HasValue ? Math.Round((log.TimeOut.Value - log.TimeIn.Value).TotalHours, 1) : 0);

                            if (string.Equals(log.Status, "Late", StringComparison.OrdinalIgnoreCase))
                            {
                                dayStatus = "Late";
                                statusClass = "k-badge-warning";
                            }
                            else if (hrs > 0 || (log.TimeIn.HasValue && log.TimeOut.HasValue))
                            {
                                dayStatus = isWk ? "Weekend Duty" : "Present";
                                statusClass = "k-badge-approved";
                            }
                            else
                            {
                                dayStatus = log.Status ?? "Present";
                                statusClass = "k-badge-approved";
                            }
                        }
                        else if (isWk)
                        {
                            dayStatus = "Weekend";
                            statusClass = "k-badge-secondary";
                        }

                        recentFiveDays.Add(new EmployeeAttendanceLogItem
                        {
                            DateStr = checkDate.ToString("MMM dd"),
                            DayName = checkDate.ToString("ddd"),
                            TimeIn = timeIn,
                            TimeOut = timeOut,
                            TotalHours = hrs,
                            Status = dayStatus,
                            StatusClass = statusClass
                        });
                    }
                    RecentAttendanceLogs = recentFiveDays;
                }
                else
                {
                    // =========================================================================
                    // LOAD MANAGER / CORPORATE DASHBOARD DATA
                    // =========================================================================
                    int? scopedBranchId = null;
                    List<int>? amBranchIds = null;

                    if (User.IsInRole("Branch Manager") && !string.IsNullOrWhiteSpace(currentUser?.Branch))
                    {
                        var branch = await _context.Branches.FirstOrDefaultAsync(b => b.Name == currentUser.Branch);
                        scopedBranchId = branch?.Id;
                    }
                    else if (User.IsInRole("Department Head") && !string.IsNullOrWhiteSpace(currentUser?.Branch))
                    {
                        var branch = await _context.Branches.FirstOrDefaultAsync(b => b.Name == currentUser.Branch);
                        scopedBranchId = branch?.Id;
                    }
                    else if ((User.IsInRole("Area Manager") || User.IsInRole("HR Officer")) && !string.IsNullOrWhiteSpace(currentUser?.ManagedBranches))
                    {
                        amBranchIds = currentUser.ManagedBranches
                            .Split(',', StringSplitOptions.RemoveEmptyEntries)
                            .Select(s => int.TryParse(s.Trim(), out var id) ? id : 0)
                            .Where(id => id > 0).ToList();
                    }

                    var countQuery = _context.Employees
                        .Where(e => !e.NIC.StartsWith("DUTY") && e.NIC != "DUTY-ACC" && e.Status != "Draft" && e.Status != "Terminated" && e.Status != "Resigned");

                    if (scopedBranchId.HasValue)
                        countQuery = countQuery.Where(e => e.BranchId == scopedBranchId.Value);
                    else if (amBranchIds != null)
                        countQuery = countQuery.Where(e => amBranchIds.Contains(e.BranchId));

                    TotalEmployees = await countQuery.CountAsync();

                    // Real Count of Employees On Leave Today
                    var today = SriLankaTime.Today;
                    var onLeaveQuery = _context.Leaves
                        .Where(l => l.Status == "Approved" && l.StartDate.Date <= today && l.EndDate.Date >= today);

                    if (scopedBranchId.HasValue)
                        onLeaveQuery = onLeaveQuery.Where(l => l.Employee.BranchId == scopedBranchId.Value);
                    else if (amBranchIds != null)
                        onLeaveQuery = onLeaveQuery.Where(l => amBranchIds.Contains(l.Employee.BranchId));

                    OnLeaveToday = await onLeaveQuery.CountAsync();

                    // Pending Approvals
                    List<PendingApprovalItem> approvalsList = new();
                    if (User.IsInRole("Welfare Manager"))
                    {
                        var pendingWelfare = await _context.WelfareRequests
                            .Include(w => w.Employee)
                            .Include(w => w.WelfareType)
                            .Where(w => w.CurrentLevel == "DepartmentHead" && w.CurrentStatus == "Pending" && w.Employee != null && !w.Employee.NIC.StartsWith("DUTY"))
                            .OrderByDescending(w => w.CreatedAt)
                            .ToListAsync();

                        PendingRequests = pendingWelfare.Count;
                        approvalsList = pendingWelfare.Take(5).Select(w => new PendingApprovalItem
                        {
                            EmployeeName = w.Employee?.FullName ?? "Staff Member",
                            RequestType = $"{w.WelfareType?.TypeName ?? "Welfare"} Request",
                            DateRange = w.CreatedAt.ToString("MMM dd, yyyy"),
                            Status = "Pending Review",
                            RequestId = w.RequestId,
                            ActionUrl = "/Welfare/Approvals/DepartmentHeadApproval"
                        }).ToList();
                    }
                    else
                    {
                        List<Application.Models.TransferRequestViewModel> pending = new();
                        if (User.IsInRole("Area Manager"))
                            pending = await _transferService.GetRequestsForAreaManagerAsync();
                        else if (User.IsInRole("HR Manager"))
                            pending = await _transferService.GetRequestsForHRManagerAsync(currentUser?.Branch ?? "");
                        else if (User.IsInRole("Branch Manager"))
                            pending = await _transferService.GetPendingRequestsForBranchManagerAsync(currentUser?.Branch ?? "");
                        else if (User.IsInRole("Employee"))
                            pending = await _transferService.GetRequestsByUserAsync(email ?? "");

                        PendingRequests = pending.Count;
                        approvalsList = pending.Take(5).Select(r => new PendingApprovalItem
                        {
                            EmployeeName = r.EmployeeName,
                            RequestType = "Transfer Request",
                            DateRange = r.RequestedDate.ToString("MMM dd, yyyy"),
                            Status = "Pending",
                            RequestId = r.Id,
                            ActionUrl = $"/Transfer/Details/{r.Id}"
                        }).ToList();
                    }

                    PendingApprovals = approvalsList;

                    // Open Positions / Active Trainings
                    OpenPositions = await _context.Trainings.CountAsync(t => t.Status == "Scheduled" && t.Date >= SriLankaTime.Today);
                }

                // =========================================================================
                // COMMON UPCOMING EVENTS (CALENDAR + TRAININGS)
                // =========================================================================
                UpcomingEvents = new List<UpcomingEventItem>();
                var currentUserId = currentUser?.Id ?? "";
                var currentEmpId = employee?.Id ?? currentUser?.EmployeeId ?? 0;
                var currentEmpBranchId = employee?.BranchId;
                var currentEmpDeptId = employee?.DepartmentId;

                // 1. Fetch CalendarEvents created by this user
                var userEvents = await _context.CalendarEvents
                    .Where(e => e.CreatedByUserId == currentUserId && e.StartTime >= now)
                    .OrderBy(e => e.StartTime)
                    .Take(4)
                    .ToListAsync();

                // 2. Fetch Trainings scoped strictly by user role
                var trainingQuery = _context.Trainings
                    .Include(t => t.EmployeeTrainings)
                        .ThenInclude(et => et.Employee)
                    .Where(t => t.Status == "Scheduled" && t.Date >= now.Date)
                    .AsQueryable();

                if (!isManagerOrAdmin)
                {
                    // Regular Employee: ONLY see trainings where directly enrolled
                    trainingQuery = trainingQuery.Where(t => t.EmployeeTrainings.Any(et => et.EmployeeId == currentEmpId));
                }
                else if (User.IsInRole("Branch Manager"))
                {
                    int? bmBranchId = null;
                    if (!string.IsNullOrWhiteSpace(currentUser?.Branch))
                    {
                        var b = await _context.Branches.FirstOrDefaultAsync(x => x.Name == currentUser.Branch);
                        bmBranchId = b?.Id;
                    }
                    bmBranchId ??= currentEmpBranchId;

                    if (bmBranchId.HasValue)
                    {
                        trainingQuery = trainingQuery.Where(t => t.EmployeeTrainings.Any(et => et.Employee != null && et.Employee.BranchId == bmBranchId.Value));
                    }
                }
                else if (User.IsInRole("Area Manager") || User.IsInRole("HR Officer"))
                {
                    List<int> managedBranchIds = new();
                    if (!string.IsNullOrWhiteSpace(currentUser?.ManagedBranches))
                    {
                        managedBranchIds = currentUser.ManagedBranches
                            .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                            .Select(s => int.TryParse(s, out var bid) ? bid : 0)
                            .Where(bid => bid > 0)
                            .ToList();
                    }

                    if (managedBranchIds.Count > 0)
                    {
                        trainingQuery = trainingQuery.Where(t => t.EmployeeTrainings.Any(et => et.Employee != null && managedBranchIds.Contains(et.Employee.BranchId)));
                    }
                    else if (currentEmpBranchId.HasValue)
                    {
                        trainingQuery = trainingQuery.Where(t => t.EmployeeTrainings.Any(et => et.Employee != null && et.Employee.BranchId == currentEmpBranchId.Value));
                    }
                }
                else if (User.IsInRole("Department Head"))
                {
                    if (currentEmpDeptId.HasValue)
                    {
                        trainingQuery = trainingQuery.Where(t => t.EmployeeTrainings.Any(et => et.Employee != null && et.Employee.DepartmentId == currentEmpDeptId.Value));
                    }
                }

                var userTrainings = await trainingQuery
                    .OrderBy(t => t.Date)
                    .Take(4)
                    .ToListAsync();

                var combinedEvents = new List<(DateTime SortDate, UpcomingEventItem Item)>();

                foreach (var e in userEvents)
                {
                    var theme = e.EventType switch
                    {
                        "Training" => "event-green",
                        "Meeting" => "event-blue",
                        "Personal" => "event-orange",
                        _ => "event-green"
                    };
                    combinedEvents.Add((e.StartTime, new UpcomingEventItem
                    {
                        Title = e.Title,
                        Time = e.IsAllDay ? "All Day" : $"{e.StartTime:hh:mm tt} - {e.EndTime:hh:mm tt}",
                        Month = e.StartTime.ToString("MMM"),
                        Day = e.StartTime.Day.ToString(),
                        ThemeClass = theme
                    }));
                }

                foreach (var t in userTrainings)
                {
                    var startDt = t.Date.Date.Add(t.StartTime);
                    if (!combinedEvents.Any(c => c.Item.Title.Contains(t.Title)))
                    {
                        combinedEvents.Add((startDt, new UpcomingEventItem
                        {
                            Title = $"[Training] {t.Title}",
                            Time = $"{startDt:hh:mm tt} ({t.DurationHours}h)",
                            Month = t.Date.ToString("MMM"),
                            Day = t.Date.Day.ToString(),
                            ThemeClass = "event-green"
                        }));
                    }
                }

                UpcomingEvents = combinedEvents
                    .OrderBy(c => c.SortDate)
                    .Select(c => c.Item)
                    .Take(4)
                    .ToList();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[Index.cshtml.cs] OnGet error: {ex}");
                GreetingName = User.Identity?.Name ?? "User";
                UpcomingEvents = new List<UpcomingEventItem>();
            }

            return Page();
        }

        public class EmployeeProfileInfo
        {
            public int Id { get; set; }
            public string FullName { get; set; } = "";
            public string NameWithInitials { get; set; } = "";
            public string EmpCode { get; set; } = "";
            public string DepartmentName { get; set; } = "";
            public string DesignationTitle { get; set; } = "";
            public string BranchName { get; set; } = "";
            public string AvatarInitial { get; set; } = "E";
        }

        public class EmployeeRequestItem
        {
            public string Category { get; set; } = "";
            public string Title { get; set; } = "";
            public string SubmittedDate { get; set; } = "";
            public string DateRange { get; set; } = "";
            public string Status { get; set; } = "";
            public string BadgeClass { get; set; } = "st-pending";
            public string ActionUrl { get; set; } = "#";
        }

        public class EmployeeAttendanceLogItem
        {
            public string DateStr { get; set; } = "";
            public string DayName { get; set; } = "";
            public string TimeIn { get; set; } = "—";
            public string TimeOut { get; set; } = "—";
            public double TotalHours { get; set; }
            public string Status { get; set; } = "";
            public string StatusClass { get; set; } = "k-badge-secondary";
        }

        public class PendingApprovalItem
        {
            public int RequestId { get; set; }
            public string EmployeeName { get; set; } = "";
            public string RequestType { get; set; } = "";
            public string DateRange { get; set; } = "";
            public string Status { get; set; } = "";
            public string ActionUrl { get; set; } = "";
        }

        public class UpcomingEventItem
        {
            public string Title { get; set; } = "";
            public string Time { get; set; } = "";
            public string Month { get; set; } = "";
            public string Day { get; set; } = "";
            public string ThemeClass { get; set; } = "";
        }
    }
}
