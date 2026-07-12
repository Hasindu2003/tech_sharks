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

            var todayLogCount = await _context.BiometricLogs
                .CountAsync(x => x.EmployeeId == biometricLog.EmployeeId && x.LogDateTime.Date == date);

            biometricLog.LogType = todayLogCount % 2 == 0 ? "checkIn" : "checkOut";
            
            _context.BiometricLogs.Add(biometricLog);
            await _context.SaveChangesAsync();
            _logger.LogInformation("BiometricLog saved - Id: {Id}, Type: {LogType}", biometricLog.Id, biometricLog.LogType);

            var attendance = await _context.Attendances
                .FirstOrDefaultAsync(a => a.EmployeeId == biometricLog.EmployeeId && a.Date == date);

            if (attendance == null)
            {
                attendance = new Attendance
                {
                    EmployeeId = biometricLog.EmployeeId,
                    Date = date,
                    TimeIn = biometricLog.LogDateTime,
                    Status = "Present"
                };
                _context.Attendances.Add(attendance);
                _logger.LogInformation("Attendance created - First CheckIn at {TimeIn}", attendance.TimeIn);
            }
            else
            {
                attendance.TimeOut = biometricLog.LogDateTime;
                if (attendance.TimeIn.HasValue)
                {
                    attendance.TotalHours = (biometricLog.LogDateTime - attendance.TimeIn.Value).TotalHours;
                }
                _logger.LogInformation("Attendance updated - TimeOut: {TimeOut}, TotalHours: {Hours}", 
                    attendance.TimeOut, attendance.TotalHours?.ToString("F2"));
            }

            await _context.SaveChangesAsync();
        }
    }
}
