using System;
using System.Threading.Tasks;
using HRMS.Domain.DTOs;
using HRMS.Domain.Entities.Attendance;
using HRMS.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace HRMS.UI.Services.Impl
{
    public class BiometricLogService : IBiometricLogService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<BiometricLogService> _logger;
        private readonly IAttendanceService _attendanceService;

        public BiometricLogService(ApplicationDbContext context, ILogger<BiometricLogService> logger, IAttendanceService attendanceService)
        {
            _context = context;
            _logger = logger;
            _attendanceService = attendanceService;
        }

        public async Task<BiometricLogResponseDto> CreateLogAsync(BiometricLogDto createDto)
        {
            var employee = await _context.Employees.FindAsync(createDto.EmployeeId);
            if (employee == null)
            {
                throw new Exception($"Employee with id {createDto.EmployeeId} not found");
            }

            var biometricLog = new BiometricLog
            {
                EmployeeId = createDto.EmployeeId,
                LogDateTime = createDto.LogDateTime,
                DeviceId = createDto.DeviceId,
                LogType = createDto.LogType ?? "checkIn",
            };

            await _attendanceService.ProcessAttendanceAsync(biometricLog);

            return new BiometricLogResponseDto
            {
                Id = biometricLog.Id,
                EmployeeId = biometricLog.EmployeeId,
                LogDateTime = biometricLog.LogDateTime,
                DeviceId = biometricLog.DeviceId,
                LogType = biometricLog.LogType ?? "checkIn",
            };
        }
    }
}
