namespace HRMS.Domain.Entities.Leave
{
    // One configurable row per LeaveType — company-wide policy, editable by HR.
    public class LeavePolicy
    {
        public int Id { get; set; }

        public LeaveType LeaveType { get; set; }
        public string Name { get; set; } = null!;

        // Null means unlimited (e.g. No Pay Leave).
        public int? DaysPerYear { get; set; }

        public bool IsPaid { get; set; }

        // False for leave types that don't reduce the employee's balance (e.g. No Pay Leave).
        public bool AffectsBalance { get; set; } = true;

        public bool RequiresAttachment { get; set; }
        public bool AllowHalfDay { get; set; } = true;

        public bool ExcludeWeekends { get; set; } = true;
        public bool ExcludeHolidays { get; set; } = true;
        public bool AllowPastDates { get; set; }

        public bool CarryForwardAllowed { get; set; }
        public int? MaxCarryForwardDays { get; set; }

        public bool Active { get; set; } = true;
    }
}
