using System.ComponentModel.DataAnnotations;

namespace HRMS.Application.Models
{
    public class TerminationRequestViewModel
    {
        public int Id { get; set; }

        // ── Employee Details ──
        [Display(Name = "Employee Name")]
        public string EmployeeName { get; set; } = string.Empty;

        [Display(Name = "EPF Number")]
        public string EpfNumber { get; set; } = string.Empty;

        [Display(Name = "Employee Email")]
        public string EmployeeEmail { get; set; } = string.Empty;

        [Display(Name = "Branch")]
        public string Branch { get; set; } = string.Empty;

        [Display(Name = "Department")]
        public string Department { get; set; } = string.Empty;

        [Display(Name = "Designation")]
        public string Designation { get; set; } = string.Empty;

        // ── Termination Details ──
        [Required(ErrorMessage = "Termination type is required.")]
        [Display(Name = "Termination Type")]
        public TerminationTypeEnum TerminationType { get; set; }

        [Required(ErrorMessage = "Reason for termination is required.")]
        [StringLength(1000, MinimumLength = 10, ErrorMessage = "Reason must be between 10 and 1000 characters.")]
        [Display(Name = "Reason for Termination")]
        public string ReasonForTermination { get; set; } = string.Empty;

        [Required(ErrorMessage = "Initiation date is required.")]
        [DataType(DataType.Date)]
        [Display(Name = "Initiation Date")]
        public DateTime InitiationDate { get; set; }

        [Required(ErrorMessage = "Effective termination date is required.")]
        [DataType(DataType.Date)]
        [Display(Name = "Effective Termination Date")]
        public DateTime EffectiveTerminationDate { get; set; }

        [StringLength(1000)]
        [Display(Name = "Supervisor Remarks")]
        public string? SupervisorRemarks { get; set; }

        [StringLength(1000)]
        [Display(Name = "Special Remarks / Notes")]
        public string? SpecialRemarks { get; set; }

        // ── Obligations ──
        [StringLength(2000)]
        [Display(Name = "Direct Obligations (e.g. outstanding loans)")]
        public string? DirectObligations { get; set; }

        [StringLength(2000)]
        [Display(Name = "Indirect Obligations (e.g. loan guarantor)")]
        public string? IndirectObligations { get; set; }

        public bool HasOutstandingLoans { get; set; }
        public bool IsLoanGuarantor { get; set; }
        public bool HasOverridePermission { get; set; }

        // ── Workflow ──
        public TerminationStatusEnum Status { get; set; }
        public string InitiatedBy { get; set; } = string.Empty;
        public string InitiatedByRole { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; }
        public DateTime LastModifiedDate { get; set; }

        // ── Stage 3: Branch Manager Review ──
        public string? BMReview { get; set; }
        public DateTime? BMReviewDate { get; set; }
        public string? BMComments { get; set; }
        public string? BMEmail { get; set; }

        // ── Stage 4: Area Manager Review ──
        public string? AMReview { get; set; }
        public DateTime? AMReviewDate { get; set; }
        public string? AMComments { get; set; }
        public string? AMEmail { get; set; }

        // ── Stage 5: HR Finalization ──
        public string? HRReview { get; set; }
        public DateTime? HRReviewDate { get; set; }
        public string? HRComments { get; set; }
        public string? HREmail { get; set; }

        // Backward compatibility
        public string? ApproverReview => HRReview ?? AMReview ?? BMReview;
        public string? ApprovedBy => HREmail ?? AMEmail ?? BMEmail;
        public DateTime? ApproverReviewDate => HRReviewDate ?? AMReviewDate ?? BMReviewDate;
        public string? ApproverComments => HRComments ?? AMComments ?? BMComments;

        // ── Finance Clearance ──
        public bool FinanceClearanceCompleted { get; set; }
        public DateTime? FinanceClearanceDate { get; set; }
        public string? FinanceClearanceNotes { get; set; }

        // ── Documents ──
        public List<TerminationDocumentViewModel> Documents { get; set; } = new();
        public int DocumentCount { get; set; }

        // ── Department Clearances (Stage 2) ──
        public List<TerminationDepartmentReviewViewModel> DepartmentReviews { get; set; } = new();

        public int TotalDeptHeadsCount => DepartmentReviews.Count;
        public int DeptHeadsApprovedCount => DepartmentReviews.Count(dr => dr.Status == "Approved");
        public int DeptHeadsPendingCount => DepartmentReviews.Count(dr => dr.Status == "Pending");
        public int DeptHeadsRejectedCount => DepartmentReviews.Count(dr => dr.Status == "Rejected");
        public bool AreAllDeptHeadsApproved => TotalDeptHeadsCount > 0 && DeptHeadsApprovedCount == TotalDeptHeadsCount;

        // ── Helpers ──
        public string StatusDisplay => Status switch
        {
            TerminationStatusEnum.Draft => "Draft",
            TerminationStatusEnum.SubmittedForApproval => "Pending Department Heads",
            TerminationStatusEnum.DeptHeadRejected => "Rejected by Department Head",
            TerminationStatusEnum.DeptHeadsApproved => "Dept Heads Approved - Awaiting Branch Manager",
            TerminationStatusEnum.BMApproved => "Approved by Branch Manager - Awaiting Area Manager",
            TerminationStatusEnum.BMRejected => "Rejected by Branch Manager",
            TerminationStatusEnum.AMApproved => "Approved by Area Manager - Ready for HR Finalization",
            TerminationStatusEnum.AMRejected => "Rejected by Area Manager",
            TerminationStatusEnum.HRApproved => "Finalized / Approved by HR",
            TerminationStatusEnum.HRRejected => "Rejected by HR",
            TerminationStatusEnum.FinanceClearance => "Finance Clearance",
            TerminationStatusEnum.Terminated => "Terminated",
            _ => Status.ToString()
        };

        public string StatusBadgeClass => Status switch
        {
            TerminationStatusEnum.Draft => "k-badge-secondary",
            TerminationStatusEnum.SubmittedForApproval => "k-badge-pending",
            TerminationStatusEnum.DeptHeadRejected => "k-badge-rejected",
            TerminationStatusEnum.DeptHeadsApproved => "k-badge-info",
            TerminationStatusEnum.BMApproved => "k-badge-info",
            TerminationStatusEnum.BMRejected => "k-badge-rejected",
            TerminationStatusEnum.AMApproved => "k-badge-warning",
            TerminationStatusEnum.AMRejected => "k-badge-rejected",
            TerminationStatusEnum.HRApproved => "k-badge-approved",
            TerminationStatusEnum.HRRejected => "k-badge-rejected",
            TerminationStatusEnum.FinanceClearance => "k-badge-pending",
            TerminationStatusEnum.Terminated => "k-badge-approved",
            _ => "k-badge-secondary"
        };

        public string TerminationTypeDisplay => TerminationType switch
        {
            TerminationTypeEnum.Voluntary => "Voluntary",
            TerminationTypeEnum.Involuntary => "Involuntary",
            TerminationTypeEnum.Retirement => "Retirement",
            TerminationTypeEnum.EndOfContract => "End of Contract",
            TerminationTypeEnum.Probation => "Probation",
            TerminationTypeEnum.Death => "Death",
            _ => "Unknown"
        };
    }

    public class TerminationDepartmentReviewViewModel
    {
        public int Id { get; set; }
        public int TerminationRequestId { get; set; }
        public string DepartmentName { get; set; } = string.Empty;
        public string? ReviewerUserId { get; set; }
        public string? ReviewerName { get; set; }
        public string? ReviewerEmail { get; set; }
        public string Status { get; set; } = "Pending";
        public string? Comments { get; set; }
        public DateTime? ReviewDate { get; set; }

        public string StatusBadgeClass => Status switch
        {
            "Approved" => "k-badge-approved",
            "Rejected" => "k-badge-rejected",
            _ => "k-badge-pending"
        };
    }

    public class TerminationDocumentViewModel
    {
        public int Id { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public string DocumentType { get; set; } = string.Empty;
        public DateTime UploadedDate { get; set; }
    }

    public enum TerminationStatusEnum
    {
        Draft = 0,
        SubmittedForApproval = 1, // Stage 2: Pending Dept Heads
        DeptHeadRejected = 2,
        DeptHeadsApproved = 3,    // Stage 3: Pending Branch Manager
        BMApproved = 4,           // Stage 4: Pending Area Manager
        BMRejected = 5,
        AMApproved = 6,           // Stage 5: Pending HR Finalization
        AMRejected = 7,
        HRApproved = 8,
        HRRejected = 9,
        FinanceClearance = 10,
        Terminated = 11
    }

    public enum TerminationTypeEnum
    {
        Voluntary = 0,
        Involuntary = 1,
        Retirement = 2,
        EndOfContract = 3,
        Probation = 4,
        Death = 5
    }
}
