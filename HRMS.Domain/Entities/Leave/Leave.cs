using HRMS.Domain.Entities.Core;

namespace HRMS.Domain.Entities.Leave
{
    public class Leave
    {
        public int Id { get; set; }

        public int EmployeeId { get; set; }
        public Employee Employee { get; set; } = null!;

        public LeaveType LeaveType { get; set; }

        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public bool IsHalfDay { get; set; }

        // Countable days after applying the policy's weekend/holiday exclusion rules — supports .5 for half-day.
        public decimal DaysCount { get; set; }

        public string? Reason { get; set; }
        public string? AttachmentPath { get; set; }

        public LeaveStatus Status { get; set; }
        public DateTime AppliedAt { get; set; }

        public DateTime? CancelledAt { get; set; }
        public string? CancellationReason { get; set; }

        public ICollection<LeaveApproval> Approvals { get; set; } = new List<LeaveApproval>();

        // Navigation properties to special leaves
        public MaternityLeave? MaternityLeave { get; set; }
        public OverseasLeave? OverseasLeave { get; set; }
        public MaternityPayment? MaternityPayment { get; set; }
    }
}
