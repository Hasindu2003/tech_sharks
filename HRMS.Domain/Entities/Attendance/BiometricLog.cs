using System;
using HRMS.Domain.Entities.Core;

namespace HRMS.Domain.Entities.Attendance
{
    public class BiometricLog
    {
        public int Id { get; set; }

        public int EmployeeId { get; set; }
        public Employee Employee { get; set; } = null!;

        public DateTime LogDateTime { get; set; }
        public string DeviceId { get; set; } = null!;
        public string? LogType { get; set; }
    }
}
