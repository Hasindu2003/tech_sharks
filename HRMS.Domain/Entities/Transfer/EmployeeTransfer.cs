using System;
using System.Collections.Generic;
using HRMS.Domain.Entities.Core;

namespace HRMS.Domain.Entities.Transfer
{
    public class EmployeeTransfer
    {
        public int Id { get; set; }  // PK

        // Employee being transferred
        public int EmployeeId { get; set; }
        public Employee Employee { get; set; } = null!;

        // Transfer details
        public string TransferType { get; set; } = null!;    // Promotion, Punishment, Service, etc.
        public string InitiatedBy { get; set; } = null!;     // Employee / Company
        public string? Reason { get; set; }                  // Optional explanation

        // Departments
        public int FromDepartmentId { get; set; }
        public Department FromDepartment { get; set; } = null!;
        public int ToDepartmentId { get; set; }
        public Department ToDepartment { get; set; } = null!;

        // Designations (optional)
        public int? FromDesignationId { get; set; }
        public Designation? FromDesignation { get; set; }
        public int? ToDesignationId { get; set; }
        public Designation? ToDesignation { get; set; }

        // Dates
        public DateTime RequestedDate { get; set; }
        public DateTime EffectiveDate { get; set; }

        // Overall status
        public string Status { get; set; } = "Pending";  // Pending / Approved / Rejected

        // Optional notes
        public string? Notes { get; set; }

        // Navigation property: multi-level approvals
        public ICollection<TransferApproval> Approvals { get; set; } = new List<TransferApproval>();
    }
}
