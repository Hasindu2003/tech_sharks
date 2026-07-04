namespace HRMS.Application.Attendance
{
    public class AttendanceShiftOptions
    {
        public TimeSpan OfficeStart { get; set; } = new TimeSpan(9, 0, 0);
        public TimeSpan OfficeEnd { get; set; } = new TimeSpan(18, 0, 0);
        public int GraceMinutes { get; set; } = 15;
        public double FullDayHours { get; set; } = 8;
        public double HalfDayThresholdHours { get; set; } = 4;
    }
}
