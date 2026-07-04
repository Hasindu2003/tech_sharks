namespace HRMS.Application.Attendance
{
    public class AttendanceCalculationResult
    {
        public int WorkingMinutes { get; set; }
        public int LateMinutes { get; set; }
        public int EarlyLeaveMinutes { get; set; }
        public int OvertimeMinutes { get; set; }
        public string Status { get; set; } = null!; // Present, Late, HalfDay
    }
}
