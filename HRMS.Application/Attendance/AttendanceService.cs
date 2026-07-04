using HRMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Application.Attendance
{
    public class AttendanceService : IAttendanceService
    {
        private readonly ApplicationDbContext _context;
        private readonly AttendanceShiftOptions _rules;

        public AttendanceService(ApplicationDbContext context, AttendanceShiftOptions rules)
        {
            _context = context;
            _rules = rules;
        }

        public async Task<AttendanceOperationResult<TodayAttendanceDto>> PunchInAsync(int employeeId,
            DateTime timestamp, string source)
        {
            var employeeExists = await _context.Employees.AnyAsync(e => e.Id == employeeId);
            if (!employeeExists)
                return AttendanceOperationResult<TodayAttendanceDto>.Fail("Employee not found.");

            var today = timestamp.Date;
            var existing = await _context.Attendances
                .FirstOrDefaultAsync(a => a.EmployeeId == employeeId && a.Date == today);

            if (existing != null)
            {
                return AttendanceOperationResult<TodayAttendanceDto>.Fail(
                    $"You have already punched in today at {existing.TimeIn:hh:mm tt}.");
            }

            var attendance = new Domain.Entities.Attendance.Attendance
            {
                EmployeeId = employeeId,
                Date = today,
                TimeIn = timestamp,
                CheckInSource = source,
                Status = "Working"
            };

            _context.Attendances.Add(attendance);
            await _context.SaveChangesAsync();

            return AttendanceOperationResult<TodayAttendanceDto>.Ok(ToTodayDto(attendance), "Punched in successfully.");
        }

        public async Task<AttendanceOperationResult<TodayAttendanceDto>> PunchOutAsync(int employeeId,
            DateTime timestamp, string source)
        {
            var today = timestamp.Date;
            var attendance = await _context.Attendances
                .FirstOrDefaultAsync(a => a.EmployeeId == employeeId && a.Date == today);

            if (attendance == null)
            {
                // Only checks today's row — if an employee punched in yesterday and never punched
                // out, there is no row dated "today" to find, so this also naturally covers the
                // "cannot punch out on another day" rule from the employee's perspective.
                var openFromEarlier = await _context.Attendances
                    .Where(a => a.EmployeeId == employeeId && a.TimeOut == null && a.Date != today)
                    .OrderByDescending(a => a.Date)
                    .FirstOrDefaultAsync();

                return openFromEarlier != null
                    ? AttendanceOperationResult<TodayAttendanceDto>.Fail(
                        "Cannot punch out for a previous day — contact HR for a correction.")
                    : AttendanceOperationResult<TodayAttendanceDto>.Fail("You have not punched in today.");
            }

            if (attendance.TimeOut != null)
            {
                return AttendanceOperationResult<TodayAttendanceDto>.Fail(
                    $"You have already punched out today at {attendance.TimeOut:hh:mm tt}.");
            }

            if (timestamp < attendance.TimeIn!.Value)
            {
                return AttendanceOperationResult<TodayAttendanceDto>.Fail(
                    "Punch out time cannot be before punch in time.");
            }

            var calculation = AttendanceCalculator.Calculate(attendance.TimeIn.Value, timestamp, _rules);

            attendance.TimeOut = timestamp;
            attendance.CheckOutSource = source;
            attendance.WorkingMinutes = calculation.WorkingMinutes;
            attendance.LateMinutes = calculation.LateMinutes;
            attendance.EarlyLeaveMinutes = calculation.EarlyLeaveMinutes;
            attendance.OvertimeMinutes = calculation.OvertimeMinutes;
            attendance.Status = calculation.Status;

            await _context.SaveChangesAsync();

            return AttendanceOperationResult<TodayAttendanceDto>.Ok(ToTodayDto(attendance),
                "Punched out successfully.");
        }

        public async Task<TodayAttendanceDto?> GetTodayAsync(int employeeId)
        {
            var today = DateTime.Today;
            var attendance = await _context.Attendances
                .FirstOrDefaultAsync(a => a.EmployeeId == employeeId && a.Date == today);

            return attendance == null ? null : ToTodayDto(attendance);
        }

        public async Task<List<AttendanceHistoryItemDto>> GetHistoryAsync(int employeeId, DateTime from, DateTime to)
        {
            var fromDate = from.Date;
            var toDate = to.Date;

            var records = await _context.Attendances
                .Where(a => a.EmployeeId == employeeId && a.Date >= fromDate && a.Date <= toDate)
                .OrderByDescending(a => a.Date)
                .ToListAsync();

            return records.Select(ToHistoryDto).ToList();
        }

        public async Task<MonthSummaryDto> GetMonthSummaryAsync(int employeeId, int year, int month)
        {
            var monthStart = new DateTime(year, month, 1);
            var monthEnd = monthStart.AddMonths(1).AddDays(-1);
            var lastDayToCount = monthEnd < DateTime.Today ? monthEnd : DateTime.Today;

            var records = await _context.Attendances
                .Where(a => a.EmployeeId == employeeId && a.Date >= monthStart && a.Date <= monthEnd)
                .OrderBy(a => a.Date)
                .ToListAsync();

            var summary = new MonthSummaryDto
            {
                Year = year,
                Month = month,
                Days = records.Select(ToHistoryDto).ToList()
            };

            foreach (var record in records)
            {
                switch (record.Status)
                {
                    case "Present":
                        summary.TotalPresent++;
                        break;
                    case "Late":
                        summary.TotalLate++;
                        break;
                    case "HalfDay":
                        summary.TotalHalfDay++;
                        break;
                }

                summary.TotalWorkingMinutes += record.WorkingMinutes ?? 0;
                summary.TotalOvertimeMinutes += record.OvertimeMinutes ?? 0;
            }

            var recordedDates = records.Select(r => r.Date).ToHashSet();
            for (var day = monthStart; day <= lastDayToCount; day = day.AddDays(1))
            {
                var isWeekday = day.DayOfWeek != DayOfWeek.Saturday && day.DayOfWeek != DayOfWeek.Sunday;
                if (isWeekday && !recordedDates.Contains(day))
                    summary.TotalAbsent++;
            }

            return summary;
        }

        private static TodayAttendanceDto ToTodayDto(Domain.Entities.Attendance.Attendance a) => new()
        {
            EmployeeId = a.EmployeeId,
            Date = a.Date,
            CheckIn = a.TimeIn,
            CheckOut = a.TimeOut,
            Status = a.Status,
            WorkingMinutes = a.WorkingMinutes,
            LateMinutes = a.LateMinutes,
            EarlyLeaveMinutes = a.EarlyLeaveMinutes,
            OvertimeMinutes = a.OvertimeMinutes,
            HasPunchedIn = a.TimeIn != null,
            HasPunchedOut = a.TimeOut != null
        };

        private static AttendanceHistoryItemDto ToHistoryDto(Domain.Entities.Attendance.Attendance a) => new()
        {
            Date = a.Date,
            CheckIn = a.TimeIn,
            CheckOut = a.TimeOut,
            Status = a.Status,
            WorkingMinutes = a.WorkingMinutes,
            LateMinutes = a.LateMinutes,
            EarlyLeaveMinutes = a.EarlyLeaveMinutes,
            OvertimeMinutes = a.OvertimeMinutes
        };
    }
}
