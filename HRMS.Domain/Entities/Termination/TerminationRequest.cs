using System.ComponentModel.DataAnnotations;

namespace HRMS.Domain.Entities.Termination
{
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

        // ── Stage 3: Branch Manager Review ──
        [MaxLength(50)]
        public string? BMReview { get; set; }
        public DateTime? BMReviewDate { get; set; }
        [MaxLength(1000)]
        public string? BMComments { get; set; }
        [MaxLength(256)]
        public string? BMEmail { get; set; }

        // ── Stage 4: Area Manager Review ──
        [MaxLength(50)]
        public string? AMReview { get; set; }
        public DateTime? AMReviewDate { get; set; }
        [MaxLength(1000)]
        public string? AMComments { get; set; }
        [MaxLength(256)]
        public string? AMEmail { get; set; }

        // ── Stage 5: HR Finalization ──
        [MaxLength(50)]
        public string? HRReview { get; set; }
        public DateTime? HRReviewDate { get; set; }
        [MaxLength(1000)]
        public string? HRComments { get; set; }
        [MaxLength(256)]
        public string? HREmail { get; set; }

        // ── Finance Clearance ──
        public bool FinanceClearanceCompleted { get; set; }
        public DateTime? FinanceClearanceDate { get; set; }
        [MaxLength(1000)]
        public string? FinanceClearanceNotes { get; set; }

        // ── Navigation ──
        public ICollection<TerminationDocument> Documents { get; set; } = new List<TerminationDocument>();
        public ICollection<TerminationDepartmentReview> DepartmentReviews { get; set; } = new List<TerminationDepartmentReview>();
    }

    public enum TerminationRequestStatus
    {
        Draft = 0,
        SubmittedForApproval = 1, // Stage 2: Pending Department Heads in Branch
        DeptHeadRejected = 2,
        DeptHeadsApproved = 3,    // Stage 3: Pending Branch Manager
        BMApproved = 4,           // Stage 4: Pending Area Manager
        BMRejected = 5,
        AMApproved = 6,           // Stage 5: Pending HR Officer Finalization
        AMRejected = 7,
        HRApproved = 8,           // Finalized / Approved by HR
        HRRejected = 9,
        FinanceClearance = 10,
        Terminated = 11
    }

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
