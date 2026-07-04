namespace HRMS.Application.Leave
{
    // Mirrors HRMS.Application/Attendance/AttendanceCalculator.cs — a small pure calculator
    // kept separate from the service so the day-counting rules are easy to reason about in isolation.
    public static class LeaveDayCalculator
    {
        public static List<DateTime> GetCountableDays(
            DateTime startDate,
            DateTime endDate,
            bool excludeWeekends,
            bool excludeHolidays,
            IReadOnlySet<DateTime> holidays)
        {
            var days = new List<DateTime>();

            for (var day = startDate.Date; day <= endDate.Date; day = day.AddDays(1))
            {
                if (excludeWeekends && (day.DayOfWeek == DayOfWeek.Saturday || day.DayOfWeek == DayOfWeek.Sunday))
                    continue;

                if (excludeHolidays && holidays.Contains(day))
                    continue;

                days.Add(day);
            }

            return days;
        }
    }
}
