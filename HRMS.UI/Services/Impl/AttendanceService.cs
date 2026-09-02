using System;
using System.Threading.Tasks;
using HRMS.Domain.Entities.Attendance;
using HRMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HRMS.UI.Services.Impl
{
    public class AttendanceService : IAttendanceService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AttendanceService> _logger;

        public AttendanceService(ApplicationDbContext context, ILogger<AttendanceService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task ProcessAttendanceAsync(BiometricLog biometricLog)
        {
            _logger.LogInformation("Processing biometric log - EmployeeId: {EmployeeId}, Time: {LogDateTime}", 
                biometricLog.EmployeeId, biometricLog.LogDateTime);

            var employee = await _context.Employees.FindAsync(biometricLog.EmployeeId);
            if (employee == null)
            {
                throw new Exception($"Employee with id {biometricLog.EmployeeId} not found");
            }

            var date = biometricLog.LogDateTime.Date;
            var logTime = biometricLog.LogDateTime;

            if (logTime == default(DateTime))
            {
                throw new Exception("Invalid datetime provided");
            }

            var oneMinuteAgo = logTime.AddMinutes(-1);
            var oneMinuteLater = logTime.AddMinutes(1);
            var isDuplicate = await _context.BiometricLogs
                .AnyAsync(x =>
                    x.EmployeeId == biometricLog.EmployeeId &&
                    x.LogDateTime >= oneMinuteAgo &&
                    x.LogDateTime <= oneMinuteLater);
            
            if (isDuplicate)
            {
                _logger.LogWarning("Duplicate log found, skipping");
                return;
            }

            var existingAttendance = await _context.Attendances
                .FirstOrDefaultAsync(a => a.EmployeeId == biometricLog.EmployeeId && a.Date == date);

            var todayLogCount = await _context.BiometricLogs
                .CountAsync(x => x.EmployeeId == biometricLog.EmployeeId && x.LogDateTime.Date == date);

            biometricLog.LogType = todayLogCount % 2 == 0 ? "checkIn" : "checkOut";
            
            _context.BiometricLogs.Add(biometricLog);
            await _context.SaveChangesAsync();
            _logger.LogInformation("BiometricLog saved - Id: {Id}, Type: {LogType}", biometricLog.Id, biometricLog.LogType);

            if (existingAttendance == null)
            {
                bool isLate = logTime.TimeOfDay > new TimeSpan(8, 30, 0);
                var attendance = new Attendance
                {
                    EmployeeId = biometricLog.EmployeeId,
                    Date = date,
                    TimeIn = logTime,
                    Status = isLate ? "Late" : "Present"
                };
                _context.Attendances.Add(attendance);
                _logger.LogInformation("Attendance created - CheckIn at {TimeIn}", attendance.TimeIn);
            }
            else
            {
                // Case 1: TimeIn is null or a 00:00:00 placeholder date
                if (!existingAttendance.TimeIn.HasValue || existingAttendance.TimeIn.Value.TimeOfDay == TimeSpan.Zero)
                {
                    existingAttendance.TimeIn = logTime;
                    bool isLate = logTime.TimeOfDay > new TimeSpan(8, 30, 0);
                    existingAttendance.Status = isLate ? "Late" : "Present";
                }
                // Case 2: Punch is earlier than existing TimeIn -> Update TimeIn to the earlier check-in
                else if (logTime.TimeOfDay < existingAttendance.TimeIn.Value.TimeOfDay)
                {
                    if (!existingAttendance.TimeOut.HasValue && existingAttendance.TimeIn.Value.TimeOfDay >= new TimeSpan(12, 0, 0))
                    {
                        existingAttendance.TimeOut = existingAttendance.TimeIn;
                    }
                    existingAttendance.TimeIn = logTime;
                    bool isLate = logTime.TimeOfDay > new TimeSpan(8, 30, 0);
                    existingAttendance.Status = isLate ? "Late" : "Present";
                }
                // Case 3: Punch is later than TimeIn -> Set or update TimeOut (check-out)
                else if (logTime.TimeOfDay > existingAttendance.TimeIn.Value.TimeOfDay)
                {
                    if (!existingAttendance.TimeOut.HasValue || logTime.TimeOfDay > existingAttendance.TimeOut.Value.TimeOfDay)
                    {
                        existingAttendance.TimeOut = logTime;
                    }
                }

                // Recalculate TotalHours if both TimeIn and TimeOut are valid times
                if (existingAttendance.TimeIn.HasValue && existingAttendance.TimeOut.HasValue && existingAttendance.TimeIn.Value.TimeOfDay > TimeSpan.Zero)
                {
                    existingAttendance.TotalHours = Math.Max(0, (existingAttendance.TimeOut.Value - existingAttendance.TimeIn.Value).TotalHours);
                }

                _logger.LogInformation("Attendance updated - In: {TimeIn}, Out: {TimeOut}, Hours: {Hours}", 
                    existingAttendance.TimeIn, existingAttendance.TimeOut, existingAttendance.TotalHours?.ToString("F2"));
            }

            await _context.SaveChangesAsync();
        }
    }
}
