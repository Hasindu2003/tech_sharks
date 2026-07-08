namespace HRMS.Application.Helpers
{
    public static class ServiceDurationHelper
    {
        public static string Format(DateTime? joinDate, int fallbackYears = 0)
        {
            if (joinDate.HasValue && joinDate.Value != default)
            {
                var today = DateTime.Today;
                var totalMonths = (today.Year - joinDate.Value.Year) * 12 + (today.Month - joinDate.Value.Month);
                if (totalMonths < 1) return "< 1 month";
                if (totalMonths < 12) return $"{totalMonths} month{(totalMonths == 1 ? "" : "s")}";
                var years = totalMonths / 12;
                var months = totalMonths % 12;
                return months > 0
                    ? $"{years} yr{(years == 1 ? "" : "s")} {months} month{(months == 1 ? "" : "s")}"
                    : $"{years} yr{(years == 1 ? "" : "s")}";
            }
            if (fallbackYears <= 0) return "< 1 yr";
            return $"{fallbackYears} yr{(fallbackYears == 1 ? "" : "s")}";
        }
    }
}
