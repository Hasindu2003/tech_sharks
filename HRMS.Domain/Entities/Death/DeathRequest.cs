using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace HRMS.Domain.Entities.Death
{
    public class DeathRequest
    {
        [Key]
        public int Id { get; set; }

        // ── Employee Details (auto-filled) ──
        [Required]
        [MaxLength(100)]
        public string EmployeeName { get; set; } = string.Empty;

        [Required]
        [MaxLength(20)]
        public string EpfNumber { get; set; } = string.Empty;

        [Required]
        [MaxLength(256)]
        public string EmployeeEmail { get; set; } = string.Empty;

        [Required]
        [MaxLength(200)]
        public string Branch { get; set; } = string.Empty;

        [MaxLength(100)]
        public string Department { get; set; } = string.Empty;

        [Required]
        [MaxLength(200)]
        public string Designation { get; set; } = string.Empty;

        // ── Death & Nominee Details ──
        public DateTime DateOfDeath { get; set; }
        
        [Required]
        [MaxLength(500)]
        public string NatureOfDeath { get; set; } = string.Empty;

        [Required]
        [MaxLength(200)]
        public string NomineeName { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string NomineeRelation { get; set; } = string.Empty;

        [MaxLength(100)]
        public string NomineeContact { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string? AdditionalRemarks { get; set; }

        // ── Obligations ──
        public bool HasOutstandingLoans { get; set; }
        public bool IsLoanGuarantor { get; set; }
        
        [MaxLength(2000)]
        public string? ObligationDetails { get; set; }

        // ── Workflow ──
        public DeathRequestStatus Status { get; set; }

        [Required]
        [MaxLength(256)]
        public string InitiatedBy { get; set; } = string.Empty; // HR Manager or Branch Manager

        public DateTime CreatedDate { get; set; }
        public DateTime LastModifiedDate { get; set; }

        // ── Stage 1: Branch Manager ──
        [MaxLength(50)]
        public string? BMReview { get; set; }
        public DateTime? BMReviewDate { get; set; }
        [MaxLength(1000)]
        public string? BMComments { get; set; }
        [MaxLength(256)]
        public string? BMEmail { get; set; }

        // ── Stage 2: Area Manager ──
        [MaxLength(50)]
        public string? AMReview { get; set; }
        public DateTime? AMReviewDate { get; set; }
        [MaxLength(1000)]
        public string? AMComments { get; set; }
        [MaxLength(256)]
        public string? AMEmail { get; set; }

        // ── Stage 3: HR Manager ──
        [MaxLength(50)]
        public string? HRReview { get; set; }
        public DateTime? HRReviewDate { get; set; }
        [MaxLength(1000)]
        public string? HRComments { get; set; }
        [MaxLength(256)]
        public string? HREmail { get; set; }

        // ── Post-Approval ──
        public bool AccountDeactivated { get; set; }
        public DateTime? AccountDeactivatedDate { get; set; }
        [MaxLength(256)]
        public string? AccountDeactivatedBy { get; set; }

        public bool PayrollStopped { get; set; }
        public bool FinanceClearanceTriggered { get; set; }
        
        // ── Navigation ──
        public ICollection<DeathDocument> Documents { get; set; } = new List<DeathDocument>();
    }

    public enum DeathRequestStatus
    {
        Draft = 0,
        SubmittedForApproval = 1,
        BMApproved = 2,
        BMRejected = 3,
        AMApproved = 4,
        AMRejected = 5,
        HRApproved = 6,
        HRRejected = 7,
        Completed = 8
    }
}
