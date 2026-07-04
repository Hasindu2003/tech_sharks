using HRMS.Domain.Entities.Core;

namespace HRMS.Domain.Entities.Leave
{
    // Audit trail for manual HR adjustments to an employee's leave balance.
    public class LeaveBalanceAdjustment
    {
        public int Id { get; set; }

        public int LeaveEntitlementId { get; set; }
        public LeaveEntitlement LeaveEntitlement { get; set; } = null!;

        public decimal DeltaDays { get; set; }
        public string Reason { get; set; } = null!;

        public int AdjustedByEmployeeId { get; set; }
        public Employee AdjustedByEmployee { get; set; } = null!;

        public DateTime AdjustedAt { get; set; }
    }
}
