using System;
using HRMS.Domain.Entities.Core;

namespace HRMS.Domain.Entities.Leave
{
    public class Leave
    {
        public int Id { get; set; }  // Primary Key

        public int EmployeeId { get; set; }
        public Employee Employee { get; set; } = null!;

        public DateTime AppliedDate { get; set; } = DateTime.Now;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public double TotalDays { get; set; }
        public bool IsHalfDay { get; set; } = false;
        public string? HalfDaySession { get; set; } // "First Half (Morning)" or "Second Half (Afternoon)"

        public string LeaveType { get; set; } = null!;  // Normal, Maternity, Overseas
        public string Status { get; set; } = null!;     // Pending / Approved / Rejected
        public string? Reason { get; set; }
        public string? AttachmentPath { get; set; }
        public string? RejectionReason { get; set; }

        public int? ApprovedById { get; set; }
        public Employee? ApprovedBy { get; set; }
        public DateTime? ApprovedDate { get; set; }

        // Navigation properties to special leaves
        public MaternityLeave? MaternityLeave { get; set; }
        public OverseasLeave? OverseasLeave { get; set; }
        public MaternityPayment? MaternityPayment { get; set; }
    }
}
