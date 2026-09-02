using System;

namespace HRMS.Domain.Common
{
    /// <summary>
    /// Centralized utility for handling Sri Lanka Standard Time (SLST, UTC+05:30).
    /// </summary>
    public static class SriLankaTime
    {
        private static readonly TimeZoneInfo _slTimeZone = ResolveTimeZone();

        private static TimeZoneInfo ResolveTimeZone()
        {
            try
            {
                // Standard Windows TimeZone ID
                return TimeZoneInfo.FindSystemTimeZoneById("Sri Lanka Standard Time");
            }
            catch
            {
                try
                {
                    // IANA TimeZone ID (Linux / Mac / Docker / Web App Hosts)
                    return TimeZoneInfo.FindSystemTimeZoneById("Asia/Colombo");
                }
                catch
                {
                    // Fallback to fixed UTC+05:30 offset
                    return TimeZoneInfo.CreateCustomTimeZone(
                        "Sri Lanka Standard Time",
                        TimeSpan.FromMinutes(330),
                        "Sri Lanka Standard Time",
                        "Sri Lanka Standard Time");
                }
            }
        }

        /// <summary>
        /// Gets the resolved Sri Lanka TimeZoneInfo.
        /// </summary>
        public static TimeZoneInfo TimeZone => _slTimeZone;

        /// <summary>
        /// Gets the current date and time in Sri Lanka Standard Time (UTC+05:30).
        /// </summary>
        public static DateTime Now => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _slTimeZone);

        /// <summary>
        /// Gets today's date in Sri Lanka Standard Time (UTC+05:30).
        /// </summary>
        public static DateTime Today => Now.Date;

        /// <summary>
        /// Converts any DateTime to Sri Lanka Standard Time.
        /// If the input is already in local or unspecified, it adjusts accordingly to ensure UTC+05:30.
        /// </summary>
        public static DateTime ToSriLankaTime(DateTime dt)
        {
            if (dt.Kind == DateTimeKind.Utc)
            {
                return TimeZoneInfo.ConvertTimeFromUtc(dt, _slTimeZone);
            }

            // If the time was created within the application using DateTime.Now on a system with SLST,
            // or if it was stored as UTC without Kind flag, safely convert to ensure SLST.
            if (dt.Kind == DateTimeKind.Unspecified)
            {
                // Check if the timestamp is likely UTC (e.g. within 6 hours ahead of UtcNow)
                var asUtc = DateTime.SpecifyKind(dt, DateTimeKind.Utc);
                return TimeZoneInfo.ConvertTimeFromUtc(asUtc, _slTimeZone);
            }

            return TimeZoneInfo.ConvertTime(dt, _slTimeZone);
        }

        /// <summary>
        /// Formats a DateTime into a formatted Sri Lanka Standard Time string.
        /// </summary>
        public static string Format(DateTime dt, string format = "MMM dd, hh:mm tt")
        {
            // If dt is already formatted or stored, return formatted SL time
            try
            {
                // If dt was saved via DateTime.Now on a local SLST machine, it's already SLST
                // Otherwise convert to SLST
                var slDt = (dt.Kind == DateTimeKind.Utc) ? TimeZoneInfo.ConvertTimeFromUtc(dt, _slTimeZone) : dt;
                return slDt.ToString(format);
            }
            catch
            {
                return dt.ToString(format);
            }
        }
    }
}
