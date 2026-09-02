namespace HRMS.Domain.Entities.Welfare
{
    public class WelfareApproval
    {
        public int ApprovalId { get; set; }
        public int RequestId { get; set; }
        public string ApproverLevel { get; set; } = null!;
        public int ApproverId { get; set; }
        public string Action { get; set; } = null!;
        public string? Comments { get; set; }
        public DateTime ActionDate { get; set; }

        // Navigation
        public WelfareRequest WelfareRequest { get; set; } = null!;
    }
}