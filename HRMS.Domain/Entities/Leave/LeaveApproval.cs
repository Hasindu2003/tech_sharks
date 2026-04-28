using System;
using HRMS.Domain.Entities.Core;

namespace HRMS.Domain.Entities.Leave
{
    public class LeaveApproval
    {
        public int Id { get; set; }

        // The employee who requested the leave
        public int EmployeeId { get; set; }
        public Employee Employee { get; set; } = null!;

        // The leave request being approved
        public int LeaveId { get; set; }
        public Leave Leave { get; set; } = null!;

        // The approver (an employee in the system)
        public int ApproverId { get; set; }
        public Employee Approver { get; set; } = null!;

        public string Status { get; set; } = null!;      // Pending / Approved / Rejected
        public string? Comments { get; set; }
        public DateTime ApprovalDate { get; set; }
    }
}
