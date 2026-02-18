using System;
using HRMS.Domain.Entities.Core;

namespace HRMS.Domain.Entities.Leave
{
    public class Leave
    {
        public int Id { get; set; }  // Primary Key

        public int EmployeeId { get; set; }
        public Employee Employee { get; set; } = null!;

        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }

        public string LeaveType { get; set; } = null!;  // Normal, Maternity, Overseas
        public string Status { get; set; } = null!;     // Pending / Approved / Rejected
        public string? Reason { get; set; }

        // Navigation properties to special leaves
        public MaternityLeave? MaternityLeave { get; set; }
        public OverseasLeave? OverseasLeave { get; set; }
        public MaternityPayment? MaternityPayment { get; set; }
    }
}
