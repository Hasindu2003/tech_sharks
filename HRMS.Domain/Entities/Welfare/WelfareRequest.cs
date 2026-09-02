using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using HRMS.Domain.Entities.Core;

namespace HRMS.Domain.Entities.Welfare
{
    public class WelfareRequest
    {
        public int RequestId { get; set; }              // Primary Key (request_id)
        public int EmployeeId { get; set; }             // FK (employee_id)
        public int WelfareTypeId { get; set; }          // FK (welfare_type_id)
        public DateTime RequestDate { get; set; }       // request_date
        public decimal RequestedAmount { get; set; }    // requested_amount
        public decimal? ApprovedAmount { get; set; }    // approved_amount
        public string? Remark { get; set; }             // remark
        public string Status { get; set; } = null!;     // status (Pending/Approved/Rejected/Draft)
        public bool IsDraft { get; set; } = false;      // is_draft
        public DateTime CreatedAt { get; set; }         // created_at
        public int SubmittedBy { get; set; }            // submitted_by
        public string CurrentLevel { get; set; } = "DepartmentHead";
        public string CurrentStatus { get; set; } = "Pending";

        // Navigation properties
        public Employee Employee { get; set; } = null!;
        public WelfareType WelfareType { get; set; } = null!;

        [InverseProperty(nameof(WelfareDocument.WelfareRequest))]
        public ICollection<WelfareDocument> Documents { get; set; } = new List<WelfareDocument>();
    }
}
