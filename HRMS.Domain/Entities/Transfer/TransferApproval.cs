using System;
using HRMS.Domain.Entities.Core;

namespace HRMS.Domain.Entities.Transfer
{
    public class TransferApproval
    {
        public int Id { get; set; }  // PK

        // Link to the transfer request
        public int EmployeeTransferId { get; set; }
        public EmployeeTransfer EmployeeTransfer { get; set; } = null!;

        // Approver info
        public int ApproverId { get; set; }
        public Employee Approver { get; set; } = null!;

        // Approval details
        public string Status { get; set; } = "Pending";  // Pending / Approved / Rejected
        public string? Comments { get; set; }            // Optional notes
        public DateTime ApprovalDate { get; set; }

        // Optional: Approval level
        public int Level { get; set; }  // 1 = Department Head, 2 = Branch Manager, etc.
    }
}
