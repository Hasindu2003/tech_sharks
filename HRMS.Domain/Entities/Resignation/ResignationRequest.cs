using System.ComponentModel.DataAnnotations;

namespace HRMS.Domain.Entities.Resignation
{
    public class ResignationRequest
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

        // ── Resignation Details ──
        [Required]
        [MaxLength(1000)]
        public string ReasonForResignation { get; set; } = string.Empty;

        public DateTime ResignationDate { get; set; }       // date employee submitted
        public DateTime EffectiveDate { get; set; }         // last working day

        public int NoticePeriodDays { get; set; }           // auto-calculated

        [MaxLength(1000)]
        public string? AdditionalRemarks { get; set; }

        // ── Obligations ──
        public bool HasOutstandingLoans { get; set; }
        public bool IsLoanGuarantor { get; set; }
        public bool HasOverridePermission { get; set; }

        [MaxLength(2000)]
        public string? ObligationDetails { get; set; }

        // ── Workflow ──
        public ResignationStatus Status { get; set; }

        [Required]
        [MaxLength(256)]
        public string InitiatedBy { get; set; } = string.Empty;   // employee email

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
        public bool AcceptanceLetterGenerated { get; set; }
        public DateTime? AcceptanceLetterDate { get; set; }

        public bool AccountDeactivated { get; set; }
        public DateTime? AccountDeactivatedDate { get; set; }
        [MaxLength(256)]
        public string? AccountDeactivatedBy { get; set; }

        // ── Navigation ──
        public ICollection<ResignationDocument> Documents { get; set; } = new List<ResignationDocument>();
        public ICollection<ResignationDepartmentReview> DepartmentReviews { get; set; } = new List<ResignationDepartmentReview>();
    }

    public enum ResignationStatus
    {
        Draft = 0,
        SubmittedForApproval = 1,  // Pending Department Heads
        DeptHeadRejected = 2,
        DeptHeadsApproved = 3,     // Awaiting Branch Manager
        BMApproved = 4,            // Awaiting Area Manager
        BMRejected = 5,
        AMApproved = 6,            // Awaiting HR Finalization
        AMRejected = 7,
        HRApproved = 8,            // Finalized by HR
        HRRejected = 9,
        Completed = 10             // Account Deactivated
    }
}
