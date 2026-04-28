using System.ComponentModel.DataAnnotations;

namespace HRMS.UI.Models
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

        // ── Approval ──
        public string? ApproverReview { get; set; }
        public DateTime? ApproverReviewDate { get; set; }
        public string? ApproverComments { get; set; }
        public string? ApprovedBy { get; set; }

        // ── Finance Clearance ──
        public bool FinanceClearanceCompleted { get; set; }
        public DateTime? FinanceClearanceDate { get; set; }
        public string? FinanceClearanceNotes { get; set; }

        // ── Documents ──
        public List<TerminationDocumentViewModel> Documents { get; set; } = new();
        public int DocumentCount { get; set; }

        // ── Helpers ──
        public string StatusDisplay => Status switch
        {
            TerminationStatusEnum.New => "New",
            TerminationStatusEnum.SubmittedForApproval => "Submitted for Approval",
            TerminationStatusEnum.Approved => "Approved",
            TerminationStatusEnum.Rejected => "Rejected",
            TerminationStatusEnum.FinanceClearance => "Finance Clearance",
            TerminationStatusEnum.Terminated => "Terminated",
            _ => "Unknown"
        };

        public string StatusBadgeClass => Status switch
        {
            TerminationStatusEnum.New => "k-badge-info",
            TerminationStatusEnum.SubmittedForApproval => "k-badge-pending",
            TerminationStatusEnum.Approved => "k-badge-approved",
            TerminationStatusEnum.Rejected => "k-badge-rejected",
            TerminationStatusEnum.FinanceClearance => "k-badge-pending",
            TerminationStatusEnum.Terminated => "k-badge-terminated",
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
        New = 0,
        SubmittedForApproval = 1,
        Approved = 2,
        Rejected = 3,
        FinanceClearance = 4,
        Terminated = 5
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
