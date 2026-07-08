using System.ComponentModel.DataAnnotations;
using HRMS.Domain.Entities.Death;

namespace HRMS.Application.Models
{
    public class DeathRequestViewModel
    {
        public int Id { get; set; }

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

        [Required(ErrorMessage = "Date of Death is required.")]
        [DataType(DataType.Date)]
        [Display(Name = "Date of Death")]
        public DateTime DateOfDeath { get; set; }

        [Required(ErrorMessage = "Nature of Death is required.")]
        [StringLength(500, MinimumLength = 10, ErrorMessage = "Reason must be between 10 and 500 characters.")]
        [Display(Name = "Nature/Cause of Death")]
        public string NatureOfDeath { get; set; } = string.Empty;

        [Required(ErrorMessage = "Nominee Name is required.")]
        [Display(Name = "Nominee Name")]
        public string NomineeName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Nominee Relation is required.")]
        [Display(Name = "Nominee Relationship")]
        public string NomineeRelation { get; set; } = string.Empty;

        [Display(Name = "Nominee Contact number/email")]
        public string NomineeContact { get; set; } = string.Empty;

        [Display(Name = "Additional Remarks")]
        public string? AdditionalRemarks { get; set; }

        public bool HasOutstandingLoans { get; set; }
        public bool IsLoanGuarantor { get; set; }
        public string? ObligationDetails { get; set; }

        public DeathRequestStatus Status { get; set; }
        public string InitiatedBy { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; }
        public DateTime LastModifiedDate { get; set; }

        public string? BMReview { get; set; }
        public DateTime? BMReviewDate { get; set; }
        public string? BMComments { get; set; }

        public string? AMReview { get; set; }
        public DateTime? AMReviewDate { get; set; }
        public string? AMComments { get; set; }

        public string? HRReview { get; set; }
        public DateTime? HRReviewDate { get; set; }
        public string? HRComments { get; set; }

        public bool AccountDeactivated { get; set; }
        public bool PayrollStopped { get; set; }
        public bool FinanceClearanceTriggered { get; set; }

        public List<DeathDocumentViewModel> Documents { get; set; } = new();
        public int DocumentCount { get; set; }

        public string StatusDisplay => Status switch
        {
            DeathRequestStatus.Draft => "Draft",
            DeathRequestStatus.SubmittedForApproval => "Submitted",
            DeathRequestStatus.BMApproved => "BM Approved",
            DeathRequestStatus.BMRejected => "BM Rejected",
            DeathRequestStatus.AMApproved => "AM Approved",
            DeathRequestStatus.AMRejected => "AM Rejected",
            DeathRequestStatus.HRApproved => "HR Approved",
            DeathRequestStatus.HRRejected => "HR Rejected",
            DeathRequestStatus.Completed => "Completed",
            _ => "Unknown"
        };

        public string StatusBadgeClass => Status switch
        {
            DeathRequestStatus.Draft => "k-badge-secondary",
            DeathRequestStatus.SubmittedForApproval => "k-badge-pending",
            DeathRequestStatus.BMApproved => "k-badge-info",
            DeathRequestStatus.AMApproved => "k-badge-info",
            DeathRequestStatus.HRApproved => "k-badge-approved",
            DeathRequestStatus.BMRejected => "k-badge-rejected",
            DeathRequestStatus.AMRejected => "k-badge-rejected",
            DeathRequestStatus.HRRejected => "k-badge-rejected",
            DeathRequestStatus.Completed => "k-badge-approved",
            _ => "k-badge-secondary"
        };
    }

    public class DeathDocumentViewModel
    {
        public int Id { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public string DocumentType { get; set; } = string.Empty;
        public DateTime UploadedDate { get; set; }
    }
}
