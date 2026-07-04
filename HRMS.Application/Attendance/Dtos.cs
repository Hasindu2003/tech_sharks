namespace HRMS.Application.Attendance
{
    public class AttendanceOperationResult<T>
    {
        public bool Success { get; set; }
        public string Message { get; set; } = null!;
        public T? Data { get; set; }

        public static AttendanceOperationResult<T> Ok(T data, string message) =>
            new() { Success = true, Message = message, Data = data };

        public static AttendanceOperationResult<T> Fail(string message) =>
            new() { Success = false, Message = message };
    }

    public class TodayAttendanceDto
    {
        public int EmployeeId { get; set; }
        public DateTime Date { get; set; }
        public DateTime? CheckIn { get; set; }
        public DateTime? CheckOut { get; set; }
        public string Status { get; set; } = null!;
        public int? WorkingMinutes { get; set; }
        public int? LateMinutes { get; set; }
        public int? EarlyLeaveMinutes { get; set; }
        public int? OvertimeMinutes { get; set; }
        public bool HasPunchedIn { get; set; }
        public bool HasPunchedOut { get; set; }
    }

    public class AttendanceHistoryItemDto
    {
        public DateTime Date { get; set; }
        public DateTime? CheckIn { get; set; }
        public DateTime? CheckOut { get; set; }
        public string Status { get; set; } = null!;
        public int? WorkingMinutes { get; set; }
        public int? LateMinutes { get; set; }
        public int? EarlyLeaveMinutes { get; set; }
        public int? OvertimeMinutes { get; set; }
    }

    public class MonthSummaryDto
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public int TotalPresent { get; set; }
        public int TotalLate { get; set; }
        public int TotalHalfDay { get; set; }
        public int TotalAbsent { get; set; }
        public int TotalWorkingMinutes { get; set; }
        public int TotalOvertimeMinutes { get; set; }
        public List<AttendanceHistoryItemDto> Days { get; set; } = new();
    }
}
