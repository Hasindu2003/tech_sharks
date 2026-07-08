using System.ComponentModel.DataAnnotations;

namespace HRMS.Domain.Entities.Termination
{
    /// <summary>
    /// Represents a request to terminate an employee's services.
    /// Can be initiated by HR and tracks the workflow through approval and finance clearance.
    /// </summary>
    public class TerminationRequest
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

        // ── Termination Details ──
        public TerminationType TerminationType { get; set; }

        [Required]
        [MaxLength(1000)]
        public string ReasonForTermination { get; set; } = string.Empty;

        public DateTime InitiationDate { get; set; }

        public DateTime EffectiveTerminationDate { get; set; }

        [MaxLength(1000)]
        public string? SupervisorRemarks { get; set; }

        [MaxLength(1000)]
        public string? SpecialRemarks { get; set; }

        // ── Obligations ──
        [MaxLength(2000)]
        public string? DirectObligations { get; set; }

        [MaxLength(2000)]
        public string? IndirectObligations { get; set; }

        public bool HasOutstandingLoans { get; set; }
        public bool IsLoanGuarantor { get; set; }
        public bool HasOverridePermission { get; set; }

        // ── Workflow ──
        public TerminationRequestStatus Status { get; set; }

        [Required]
        [MaxLength(256)]
        public string InitiatedBy { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string InitiatedByRole { get; set; } = string.Empty;

        public DateTime CreatedDate { get; set; }
        public DateTime LastModifiedDate { get; set; }

        // ── Approval ──
        [MaxLength(50)]
        public string? ApproverReview { get; set; }
        public DateTime? ApproverReviewDate { get; set; }
        [MaxLength(1000)]
        public string? ApproverComments { get; set; }
        [MaxLength(256)]
        public string? ApprovedBy { get; set; }

        // ── Finance Clearance ──
        public bool FinanceClearanceCompleted { get; set; }
        public DateTime? FinanceClearanceDate { get; set; }
        [MaxLength(1000)]
        public string? FinanceClearanceNotes { get; set; }

        // ── Navigation ──
        public ICollection<TerminationDocument> Documents { get; set; } = new List<TerminationDocument>();
    }

    /// <summary>
    /// Defines the possible states of a termination request during its lifecycle.
    /// </summary>
    public enum TerminationRequestStatus
    {
        New = 0,
        SubmittedForApproval = 1,
        Approved = 2,
        Rejected = 3,
        FinanceClearance = 4,
        Terminated = 5
    }

    /// <summary>
    /// Specifies the nature of the employee termination (e.g., Voluntary, Involuntary).
    /// </summary>
    public enum TerminationType
    {
        Voluntary = 0,
        Involuntary = 1,
        Retirement = 2,
        EndOfContract = 3,
        Probation = 4,
        Death = 5
    }
}
