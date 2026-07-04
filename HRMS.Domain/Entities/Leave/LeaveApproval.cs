using HRMS.Domain.Entities.Core;

namespace HRMS.Domain.Entities.Leave
{
    // Append-only history record — one row per approval-workflow action taken on a Leave.
    public class LeaveApproval
    {
        public int Id { get; set; }

        public int LeaveId { get; set; }
        public Leave Leave { get; set; } = null!;

        public ApprovalStage Stage { get; set; }

        public int ActorEmployeeId { get; set; }
        public Employee ActorEmployee { get; set; } = null!;

        public ApprovalAction Action { get; set; }
        public string? Comments { get; set; }
        public DateTime ActionDate { get; set; }
    }
}
