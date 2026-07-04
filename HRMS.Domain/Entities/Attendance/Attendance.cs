using HRMS.Domain.Entities.Core;

// Reference to Employee

namespace HRMS.Domain.Entities.Attendance
{
    public class Attendance
    {
        public int Id { get; set; }

        public int EmployeeId { get; set; }
        public Employee Employee { get; set; } = null!;

        public DateTime Date { get; set; }
        public DateTime? TimeIn { get; set; }
        public DateTime? TimeOut { get; set; }

        public string Status { get; set; } = null!; //Working, Present, Late, HalfDay, Absent, on leave, etc.

        // Source of the punch, so manual entry today can be swapped for biometric later without a schema change.
        public string CheckInSource { get; set; } = "Manual"; // "Manual" | "Biometric"
        public string? CheckOutSource { get; set; } // "Manual" | "Biometric"

        public int? WorkingMinutes { get; set; }
        public int? LateMinutes { get; set; }
        public int? EarlyLeaveMinutes { get; set; }
        public int? OvertimeMinutes { get; set; }
    }
}
