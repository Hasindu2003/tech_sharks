using HRMS.Application.Attendance;
using HRMS.Application.Notifications;
using HRMS.Domain.Entities.Leave;
using HRMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Application.Leave
{
    public class LeaveService : ILeaveService
    {
        private static readonly LeaveStatus[] ActiveStatuses =
        {
            LeaveStatus.Pending, LeaveStatus.ManagerApproved, LeaveStatus.MoreInfoRequested, LeaveStatus.Approved
        };

        private readonly IAttendanceService _attendanceService;
        private readonly ApplicationDbContext _context;
        private readonly INotificationService _notificationService;

        public LeaveService(
            ApplicationDbContext context,
            IAttendanceService attendanceService,
            INotificationService notificationService)
        {
            _context = context;
            _attendanceService = attendanceService;
            _notificationService = notificationService;
        }

        public async Task<List<LeaveBalanceDto>> GetBalancesAsync(int employeeId, int year)
        {
            var policies = await _context.LeavePolicies.Where(p => p.Active).OrderBy(p => p.LeaveType).ToListAsync();
            var entitlements = await _context.LeaveEntitlements
                .Where(e => e.EmployeeId == employeeId && e.Year == year)
                .ToListAsync();

            var result = new List<LeaveBalanceDto>();
            foreach (var policy in policies)
            {
                var entitlement = entitlements.FirstOrDefault(e => e.LeaveType == policy.LeaveType);
                var allocated = entitlement?.AllocatedDays ?? policy.DaysPerYear ?? 0;
                var carried = entitlement?.CarriedForwardDays ?? 0;
                var used = entitlement?.UsedDays ?? 0;

                result.Add(new LeaveBalanceDto
                {
                    LeaveType = policy.LeaveType,
                    LeaveTypeName = policy.Name,
                    Year = year,
                    AllocatedDays = allocated,
                    CarriedForwardDays = carried,
                    UsedDays = used,
                    RemainingDays = allocated + carried - used,
                    IsUnlimited = !policy.DaysPerYear.HasValue
                });
            }

            return result;
        }

        public async Task<List<LeaveSummaryDto>> GetMyLeavesAsync(int employeeId)
        {
            var leaves = await _context.Leaves.Include(l => l.Employee)
                .Where(l => l.EmployeeId == employeeId)
                .OrderByDescending(l => l.AppliedAt)
                .ToListAsync();

            return leaves.Select(MapSummary).ToList();
        }

        public async Task<LeaveDetailsDto?> GetDetailsAsync(int leaveId)
        {
            var leave = await _context.Leaves.Include(l => l.Employee).FirstOrDefaultAsync(l => l.Id == leaveId);
            if (leave == null)
                return null;

            var history = await _context.LeaveApprovals
                .Include(a => a.ActorEmployee)
                .Where(a => a.LeaveId == leaveId)
                .OrderBy(a => a.ActionDate)
                .Select(a => new LeaveHistoryItemDto
                {
                    Stage = a.Stage,
                    ActorName = a.ActorEmployee.FirstName + " " + a.ActorEmployee.LastName,
                    Action = a.Action,
                    Comments = a.Comments,
                    ActionDate = a.ActionDate
                })
                .ToListAsync();

            return new LeaveDetailsDto { Leave = MapSummary(leave), History = history };
        }

        public async Task<LeaveOperationResult<LeaveSummaryDto>> ApplyAsync(ApplyLeaveRequest request)
        {
            var employee = await _context.Employees.FirstOrDefaultAsync(e => e.Id == request.EmployeeId);
            if (employee == null)
                return LeaveOperationResult<LeaveSummaryDto>.Fail("Employee not found.");

            var policy =
                await _context.LeavePolicies.FirstOrDefaultAsync(p => p.LeaveType == request.LeaveType && p.Active);
            if (policy == null)
                return LeaveOperationResult<LeaveSummaryDto>.Fail("This leave type is not currently available.");

            if (request.StartDate.Date > request.EndDate.Date)
                return LeaveOperationResult<LeaveSummaryDto>.Fail("Start date must be on or before end date.");

            if (request.IsHalfDay && request.StartDate.Date != request.EndDate.Date)
                return LeaveOperationResult<LeaveSummaryDto>.Fail("Half-day leave must be a single day.");

            if (request.IsHalfDay && !policy.AllowHalfDay)
                return LeaveOperationResult<LeaveSummaryDto>.Fail($"{policy.Name} does not support half-day leave.");

            if (!policy.AllowPastDates && request.StartDate.Date < DateTime.Today)
                return LeaveOperationResult<LeaveSummaryDto>.Fail("Cannot apply for leave in the past.");

            if (policy.RequiresAttachment && string.IsNullOrWhiteSpace(request.AttachmentPath))
                return LeaveOperationResult<LeaveSummaryDto>.Fail($"{policy.Name} requires an attachment.");

            if (request.LeaveType == LeaveType.Overseas &&
                (string.IsNullOrWhiteSpace(request.PassportNumber) || string.IsNullOrWhiteSpace(request.Country) ||
                 !request.PassportExpiry.HasValue))
                return LeaveOperationResult<LeaveSummaryDto>.Fail(
                    "Passport number, passport expiry, and country are required for Overseas Leave.");

            var overlaps = await _context.Leaves.AnyAsync(l =>
                l.EmployeeId == request.EmployeeId &&
                ActiveStatuses.Contains(l.Status) &&
                l.StartDate.Date <= request.EndDate.Date &&
                l.EndDate.Date >= request.StartDate.Date);
            if (overlaps)
                return LeaveOperationResult<LeaveSummaryDto>.Fail("This overlaps with an existing leave request.");

            var holidays = await GetHolidaySetAsync(request.StartDate.Year, request.EndDate.Year);
            var countableDays = request.IsHalfDay
                ? new List<DateTime> { request.StartDate.Date }
                : LeaveDayCalculator.GetCountableDays(
                    request.StartDate, request.EndDate, policy.ExcludeWeekends, policy.ExcludeHolidays, holidays);

            var daysCount = request.IsHalfDay ? 0.5m : countableDays.Count;
            if (daysCount <= 0)
                return LeaveOperationResult<LeaveSummaryDto>.Fail(
                    "The selected range has no countable leave days (check weekends/holidays).");

            if (policy.AffectsBalance && policy.DaysPerYear.HasValue)
            {
                var entitlement = await GetOrCreateEntitlementAsync(request.EmployeeId, request.LeaveType,
                    request.StartDate.Year, policy);
                if (entitlement.RemainingDays < daysCount)
                    return LeaveOperationResult<LeaveSummaryDto>.Fail(
                        $"Insufficient balance — {entitlement.RemainingDays} day(s) remaining.");
            }

            var leave = new Domain.Entities.Leave.Leave
            {
                EmployeeId = request.EmployeeId,
                LeaveType = request.LeaveType,
                StartDate = request.StartDate.Date,
                EndDate = request.EndDate.Date,
                IsHalfDay = request.IsHalfDay,
                DaysCount = daysCount,
                Reason = request.Reason,
                AttachmentPath = request.AttachmentPath,
                // No manager on file (e.g. a top-level employee) — skip straight to the HR stage.
                Status = employee.ManagerId == null ? LeaveStatus.ManagerApproved : LeaveStatus.Pending,
                AppliedAt = DateTime.Now
            };

            if (request.LeaveType == LeaveType.Maternity && request.ExpectedDeliveryDate.HasValue)
            {
                leave.MaternityLeave = new MaternityLeave
                {
                    ExpectedDeliveryDate = request.ExpectedDeliveryDate,
                    MedicalCertificate = request.AttachmentPath
                };
            }

            if (request.LeaveType == LeaveType.Overseas)
            {
                leave.OverseasLeave = new OverseasLeave
                {
                    PassportNumber = request.PassportNumber!,
                    PassportExpiry = request.PassportExpiry!.Value,
                    Country = request.Country!,
                    Purpose = request.Purpose
                };
            }

            _context.Leaves.Add(leave);
            await _context.SaveChangesAsync();

            await _notificationService.NotifyAsync(employee.Id, "Leave Applied",
                $"Your {policy.Name} request for {request.StartDate:MMM d} - {request.EndDate:MMM d} has been submitted.",
                $"/Leave/Details?id={leave.Id}");

            if (employee.ManagerId.HasValue)
            {
                await _notificationService.NotifyAsync(employee.ManagerId.Value, "Manager Approval Required",
                    $"{employee.FirstName} {employee.LastName} has requested {policy.Name}.",
                    "/Leave/ManagerApprovals");
            }
            else
            {
                foreach (var hrId in await GetHrEmployeeIdsAsync())
                {
                    await _notificationService.NotifyAsync(hrId, "HR Approval Required",
                        $"{employee.FirstName} {employee.LastName} has requested {policy.Name} (no manager on file).",
                        "/Leave/HrApprovals");
                }
            }

            leave.Employee = employee;
            return LeaveOperationResult<LeaveSummaryDto>.Ok(MapSummary(leave), "Leave request submitted.");
        }

        public async Task<List<LeaveSummaryDto>> GetPendingForManagerAsync(int managerEmployeeId)
        {
            var leaves = await _context.Leaves.Include(l => l.Employee)
                .Where(l => (l.Status == LeaveStatus.Pending || l.Status == LeaveStatus.MoreInfoRequested)
                            && l.Employee.ManagerId == managerEmployeeId)
                .OrderBy(l => l.AppliedAt)
                .ToListAsync();

            return leaves.Select(MapSummary).ToList();
        }

        public async Task<List<LeaveSummaryDto>> GetPendingForHrAsync()
        {
            var leaves = await _context.Leaves.Include(l => l.Employee)
                .Where(l => l.Status == LeaveStatus.ManagerApproved)
                .OrderBy(l => l.AppliedAt)
                .ToListAsync();

            return leaves.Select(MapSummary).ToList();
        }

        public async Task<LeaveOperationResult<LeaveSummaryDto>> ManagerActionAsync(
            int leaveId, int managerEmployeeId, ApprovalAction action, string? comments)
        {
            var leave = await _context.Leaves.Include(l => l.Employee).FirstOrDefaultAsync(l => l.Id == leaveId);
            if (leave == null)
                return LeaveOperationResult<LeaveSummaryDto>.Fail("Leave request not found.");

            if (leave.Status != LeaveStatus.Pending && leave.Status != LeaveStatus.MoreInfoRequested)
                return LeaveOperationResult<LeaveSummaryDto>.Fail(
                    "This request is no longer awaiting manager approval.");

            if (leave.Employee.ManagerId != managerEmployeeId)
                return LeaveOperationResult<LeaveSummaryDto>.Fail("You are not the manager for this employee.");

            var policy = await _context.LeavePolicies.FirstAsync(p => p.LeaveType == leave.LeaveType);

            _context.LeaveApprovals.Add(new LeaveApproval
            {
                LeaveId = leaveId,
                Stage = ApprovalStage.Manager,
                ActorEmployeeId = managerEmployeeId,
                Action = action,
                Comments = comments,
                ActionDate = DateTime.Now
            });

            leave.Status = action switch
            {
                ApprovalAction.Approved => LeaveStatus.ManagerApproved,
                ApprovalAction.Rejected => LeaveStatus.Rejected,
                ApprovalAction.InfoRequested => LeaveStatus.MoreInfoRequested,
                _ => leave.Status
            };

            await _context.SaveChangesAsync();

            if (action == ApprovalAction.Approved)
            {
                foreach (var hrId in await GetHrEmployeeIdsAsync())
                    await _notificationService.NotifyAsync(hrId, "HR Approval Required",
                        $"{leave.Employee.FirstName} {leave.Employee.LastName}'s {policy.Name} was approved by their manager and awaits HR approval.",
                        "/Leave/HrApprovals");

                await _notificationService.NotifyAsync(leave.EmployeeId, "Leave Update",
                    $"Your {policy.Name} request was approved by your manager and is now with HR.",
                    $"/Leave/Details?id={leave.Id}");
            }
            else if (action == ApprovalAction.Rejected)
            {
                await _notificationService.NotifyAsync(leave.EmployeeId, "Leave Rejected",
                    $"Your {policy.Name} request was rejected by your manager." +
                    (string.IsNullOrWhiteSpace(comments) ? "" : $" Reason: {comments}"),
                    $"/Leave/Details?id={leave.Id}");
            }
            else
            {
                await _notificationService.NotifyAsync(leave.EmployeeId, "More Information Requested",
                    $"Your manager requested more information about your {policy.Name} request." +
                    (string.IsNullOrWhiteSpace(comments) ? "" : $" {comments}"),
                    $"/Leave/Details?id={leave.Id}");
            }

            return LeaveOperationResult<LeaveSummaryDto>.Ok(MapSummary(leave), "Action recorded.");
        }

        public async Task<LeaveOperationResult<LeaveSummaryDto>> HrActionAsync(
            int leaveId, int hrEmployeeId, ApprovalAction action, string? comments)
        {
            if (action == ApprovalAction.InfoRequested)
                return LeaveOperationResult<LeaveSummaryDto>.Fail("HR can only Approve or Reject.");

            var leave = await _context.Leaves.Include(l => l.Employee).FirstOrDefaultAsync(l => l.Id == leaveId);
            if (leave == null)
                return LeaveOperationResult<LeaveSummaryDto>.Fail("Leave request not found.");

            if (leave.Status != LeaveStatus.ManagerApproved)
                return LeaveOperationResult<LeaveSummaryDto>.Fail("This request is not awaiting HR approval.");

            var policy = await _context.LeavePolicies.FirstAsync(p => p.LeaveType == leave.LeaveType);

            _context.LeaveApprovals.Add(new LeaveApproval
            {
                LeaveId = leaveId,
                Stage = ApprovalStage.HR,
                ActorEmployeeId = hrEmployeeId,
                Action = action,
                Comments = comments,
                ActionDate = DateTime.Now
            });

            if (action == ApprovalAction.Approved)
            {
                leave.Status = LeaveStatus.Approved;

                if (policy.AffectsBalance && policy.DaysPerYear.HasValue)
                {
                    var entitlement = await GetOrCreateEntitlementAsync(leave.EmployeeId, leave.LeaveType,
                        leave.StartDate.Year, policy);
                    entitlement.UsedDays += leave.DaysCount;
                }

                await _context.SaveChangesAsync();

                var holidays = await GetHolidaySetAsync(leave.StartDate.Year, leave.EndDate.Year);
                var countableDays = leave.IsHalfDay
                    ? new List<DateTime> { leave.StartDate.Date }
                    : LeaveDayCalculator.GetCountableDays(
                        leave.StartDate, leave.EndDate, policy.ExcludeWeekends, policy.ExcludeHolidays, holidays);

                foreach (var day in countableDays)
                    await _attendanceService.MarkLeaveAsync(leave.EmployeeId, day, leave.IsHalfDay);

                await _notificationService.NotifyAsync(leave.EmployeeId, "Leave Approved",
                    $"Your {policy.Name} request for {leave.StartDate:MMM d} - {leave.EndDate:MMM d} has been approved.",
                    $"/Leave/Details?id={leave.Id}");
            }
            else
            {
                leave.Status = LeaveStatus.Rejected;
                await _context.SaveChangesAsync();

                await _notificationService.NotifyAsync(leave.EmployeeId, "Leave Rejected",
                    $"Your {policy.Name} request was rejected by HR." +
                    (string.IsNullOrWhiteSpace(comments) ? "" : $" Reason: {comments}"),
                    $"/Leave/Details?id={leave.Id}");
            }

            return LeaveOperationResult<LeaveSummaryDto>.Ok(MapSummary(leave), "Action recorded.");
        }

        public async Task<LeaveOperationResult<LeaveSummaryDto>> CancelAsync(int leaveId, int employeeId, string reason)
        {
            var leave = await _context.Leaves.Include(l => l.Employee).FirstOrDefaultAsync(l => l.Id == leaveId);
            if (leave == null)
                return LeaveOperationResult<LeaveSummaryDto>.Fail("Leave request not found.");

            if (leave.EmployeeId != employeeId)
                return LeaveOperationResult<LeaveSummaryDto>.Fail("You can only cancel your own leave requests.");

            var canCancel = leave.Status == LeaveStatus.Pending || leave.Status == LeaveStatus.MoreInfoRequested ||
                            (leave.Status == LeaveStatus.Approved && leave.StartDate.Date > DateTime.Today);
            if (!canCancel)
                return LeaveOperationResult<LeaveSummaryDto>.Fail("This leave request can no longer be cancelled.");

            var wasApproved = leave.Status == LeaveStatus.Approved;
            var policy = await _context.LeavePolicies.FirstAsync(p => p.LeaveType == leave.LeaveType);

            if (wasApproved)
            {
                if (policy.AffectsBalance && policy.DaysPerYear.HasValue)
                {
                    var entitlement = await _context.LeaveEntitlements.FirstOrDefaultAsync(e =>
                        e.EmployeeId == leave.EmployeeId && e.LeaveType == leave.LeaveType &&
                        e.Year == leave.StartDate.Year);
                    if (entitlement != null)
                        entitlement.UsedDays = Math.Max(0, entitlement.UsedDays - leave.DaysCount);
                }

                var holidays = await GetHolidaySetAsync(leave.StartDate.Year, leave.EndDate.Year);
                var countableDays = leave.IsHalfDay
                    ? new List<DateTime> { leave.StartDate.Date }
                    : LeaveDayCalculator.GetCountableDays(
                        leave.StartDate, leave.EndDate, policy.ExcludeWeekends, policy.ExcludeHolidays, holidays);

                foreach (var day in countableDays)
                    await _attendanceService.UnmarkLeaveAsync(leave.EmployeeId, day);
            }

            leave.Status = LeaveStatus.Cancelled;
            leave.CancelledAt = DateTime.Now;
            leave.CancellationReason = reason;

            await _context.SaveChangesAsync();

            if (leave.Employee.ManagerId.HasValue)
                await _notificationService.NotifyAsync(leave.Employee.ManagerId.Value, "Leave Cancelled",
                    $"{leave.Employee.FirstName} {leave.Employee.LastName} cancelled their {policy.Name} request.",
                    $"/Leave/Details?id={leave.Id}");

            if (wasApproved)
                foreach (var hrId in await GetHrEmployeeIdsAsync())
                    await _notificationService.NotifyAsync(hrId, "Leave Cancelled",
                        $"{leave.Employee.FirstName} {leave.Employee.LastName} cancelled their approved {policy.Name} request.",
                        $"/Leave/Details?id={leave.Id}");

            return LeaveOperationResult<LeaveSummaryDto>.Ok(MapSummary(leave), "Leave cancelled.");
        }

        public async Task<LeaveOperationResult<LeaveBalanceDto>> AdjustBalanceAsync(
            int employeeId, LeaveType leaveType, int year, decimal deltaDays, string reason, int adjustedByEmployeeId)
        {
            var policy = await _context.LeavePolicies.FirstOrDefaultAsync(p => p.LeaveType == leaveType);
            if (policy == null)
                return LeaveOperationResult<LeaveBalanceDto>.Fail("Unknown leave type.");

            var entitlement = await GetOrCreateEntitlementAsync(employeeId, leaveType, year, policy);
            entitlement.AllocatedDays += deltaDays;

            _context.LeaveBalanceAdjustments.Add(new LeaveBalanceAdjustment
            {
                LeaveEntitlementId = entitlement.Id,
                DeltaDays = deltaDays,
                Reason = reason,
                AdjustedByEmployeeId = adjustedByEmployeeId,
                AdjustedAt = DateTime.Now
            });

            await _context.SaveChangesAsync();

            await _notificationService.NotifyAsync(employeeId, "Leave Balance Adjusted",
                $"Your {policy.Name} balance was adjusted by {deltaDays:+0.#;-0.#} day(s). Reason: {reason}",
                "/Leave");

            return LeaveOperationResult<LeaveBalanceDto>.Ok(new LeaveBalanceDto
            {
                LeaveType = leaveType,
                LeaveTypeName = policy.Name,
                Year = year,
                AllocatedDays = entitlement.AllocatedDays,
                CarriedForwardDays = entitlement.CarriedForwardDays,
                UsedDays = entitlement.UsedDays,
                RemainingDays = entitlement.RemainingDays,
                IsUnlimited = !policy.DaysPerYear.HasValue
            }, "Balance adjusted.");
        }

        private async Task<LeaveEntitlement> GetOrCreateEntitlementAsync(
            int employeeId, LeaveType leaveType, int year, LeavePolicy policy)
        {
            var entitlement = await _context.LeaveEntitlements
                .FirstOrDefaultAsync(e => e.EmployeeId == employeeId && e.LeaveType == leaveType && e.Year == year);
            if (entitlement != null)
                return entitlement;

            entitlement = new LeaveEntitlement
            {
                EmployeeId = employeeId,
                LeaveType = leaveType,
                Year = year,
                AllocatedDays = policy.DaysPerYear ?? 0,
                CarriedForwardDays = 0,
                UsedDays = 0
            };
            _context.LeaveEntitlements.Add(entitlement);
            await _context.SaveChangesAsync();
            return entitlement;
        }

        private async Task<HashSet<DateTime>> GetHolidaySetAsync(int startYear, int endYear)
        {
            var holidays = await _context.Holidays.ToListAsync();
            var set = new HashSet<DateTime>();

            for (var year = startYear; year <= endYear; year++)
            {
                foreach (var h in holidays)
                    set.Add(h.IsRecurringYearly ? new DateTime(year, h.Date.Month, h.Date.Day) : h.Date.Date);
            }

            return set;
        }

        private async Task<List<int>> GetHrEmployeeIdsAsync()
        {
            var hrRoleId = await _context.Roles.Where(r => r.Name == "HR Manager").Select(r => r.Id)
                .FirstOrDefaultAsync();
            if (hrRoleId == null)
                return new List<int>();

            var userIds = await _context.UserRoles.Where(ur => ur.RoleId == hrRoleId).Select(ur => ur.UserId)
                .ToListAsync();
            return await _context.Users
                .Where(u => userIds.Contains(u.Id) && u.EmployeeId != null)
                .Select(u => u.EmployeeId!.Value)
                .ToListAsync();
        }

        private static LeaveSummaryDto MapSummary(Domain.Entities.Leave.Leave leave) => new()
        {
            Id = leave.Id,
            EmployeeId = leave.EmployeeId,
            EmployeeName = $"{leave.Employee.FirstName} {leave.Employee.LastName}",
            LeaveType = leave.LeaveType,
            StartDate = leave.StartDate,
            EndDate = leave.EndDate,
            IsHalfDay = leave.IsHalfDay,
            DaysCount = leave.DaysCount,
            Reason = leave.Reason,
            AttachmentPath = leave.AttachmentPath,
            Status = leave.Status,
            AppliedAt = leave.AppliedAt,
            CanCancel = leave.Status == LeaveStatus.Pending || leave.Status == LeaveStatus.MoreInfoRequested ||
                        (leave.Status == LeaveStatus.Approved && leave.StartDate.Date > DateTime.Today)
        };
    }
}
