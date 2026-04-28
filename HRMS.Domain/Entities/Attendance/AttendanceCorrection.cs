using System;
using HRMS.Domain.Entities.Core;

namespace HRMS.Domain.Entities.Attendance
{
    public class AttendanceCorrection
    {
        public int Id { get; set; }

        public int EmployeeId { get; set; }
        public Employee Employee { get; set; } = null!;

        public int AttendanceId { get; set; }
        public Attendance Attendance { get; set; } = null!;

        public DateTime CorrectedTimeIn { get; set; }
        public DateTime CorrectedTimeOut { get; set; }

        public string Reason { get; set; } = null!;
        public string Status { get; set; } = null!; //pending, approved, rejected
    }
}
