namespace HRMS.Application.Attendance
{
    // Every punch — manual today, biometric later — flows through this contract.
    // "source" identifies where the timestamp came from ("Manual" | "Biometric")
    // but never changes the validation or calculation rules.
    public interface IAttendanceService
    {
        Task<AttendanceOperationResult<TodayAttendanceDto>> PunchInAsync(int employeeId, DateTime timestamp,
            string source);

        Task<AttendanceOperationResult<TodayAttendanceDto>> PunchOutAsync(int employeeId, DateTime timestamp,
            string source);

        Task<TodayAttendanceDto?> GetTodayAsync(int employeeId);

        Task<List<AttendanceHistoryItemDto>> GetHistoryAsync(int employeeId, DateTime from, DateTime to);

        Task<MonthSummaryDto> GetMonthSummaryAsync(int employeeId, int year, int month);
    }
}
