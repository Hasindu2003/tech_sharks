namespace HRMS.Application.Attendance
{
    // Pure shift-math: no DB access, so the same logic applies to manual punches
    // today and biometric device punches later.
    public static class AttendanceCalculator
    {
        public static AttendanceCalculationResult Calculate(DateTime checkIn, DateTime checkOut,
            AttendanceShiftOptions rules)
        {
            var graceCutoff = rules.OfficeStart + TimeSpan.FromMinutes(rules.GraceMinutes);
            var isLateArrival = checkIn.TimeOfDay > graceCutoff;

            // Late minutes are measured from the official start, not the grace cutoff —
            // the grace period only decides the Present/Late label.
            var lateMinutes = isLateArrival
                ? Math.Max(0, (int)(checkIn.TimeOfDay - rules.OfficeStart).TotalMinutes)
                : 0;

            var earlyLeaveMinutes = checkOut.TimeOfDay < rules.OfficeEnd
                ? Math.Max(0, (int)(rules.OfficeEnd - checkOut.TimeOfDay).TotalMinutes)
                : 0;

            var workingMinutes = Math.Max(0, (int)(checkOut - checkIn).TotalMinutes);
            var workingHours = workingMinutes / 60.0;

            var fullDayMinutes = (int)(rules.FullDayHours * 60);
            var overtimeMinutes = workingMinutes > fullDayMinutes ? workingMinutes - fullDayMinutes : 0;

            string status;
            if (workingHours < rules.HalfDayThresholdHours)
                status = "HalfDay";
            else if (isLateArrival)
                status = "Late";
            else
                status = "Present";

            return new AttendanceCalculationResult
            {
                WorkingMinutes = workingMinutes,
                LateMinutes = lateMinutes,
                EarlyLeaveMinutes = earlyLeaveMinutes,
                OvertimeMinutes = overtimeMinutes,
                Status = status
            };
        }
    }
}
