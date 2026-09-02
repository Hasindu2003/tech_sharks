using System.ComponentModel.DataAnnotations;
using HRMS.Application.Validation;

namespace HRMS.Application.Models
{
    public class TransferRequestViewModel
    {
        public int Id { get; set; }

        // ── Employee Details ──
        [Display(Name = "Employee Name")]
        public string EmployeeName { get; set; } = string.Empty;

        [Display(Name = "EPF Number")]
        public string EpfNumber { get; set; } = string.Empty;

        [Display(Name = "Employee Email")]
        public string EmployeeEmail { get; set; } = string.Empty;

        [Required, Display(Name = "Current Branch")]
        public string CurrentBranch { get; set; } = string.Empty;

        [Display(Name = "Current Designation")]
        public string CurrentDesignation { get; set; } = string.Empty;

        [Display(Name = "Department")]
        public string Department { get; set; } = string.Empty;

        // ── Transfer Details ──
        [Required(ErrorMessage = "Please select the branch you want to transfer to.")]
        [Display(Name = "Requested Branch")]
        public string RequestedBranch { get; set; } = string.Empty;

        [Required(ErrorMessage = "Reason for transfer is required.")]
        [Display(Name = "Reason for Transfer")]
        [StringLength(500, MinimumLength = 20, ErrorMessage = "Reason must be between 20 and 500 characters.")]
        public string Reason { get; set; } = string.Empty;

        [Display(Name = "Preferred Transfer Date")]
        [DataType(DataType.Date)]
        [FutureDate(MinimumDaysAhead = 7, ErrorMessage = "Preferred date must be at least 7 days from today and no more than 1 year ahead.")]
        public DateTime? PreferredDate { get; set; }

        [Display(Name = "Years of Service")]
        [Range(0, 50, ErrorMessage = "Years of service must be between 0 and 50.")]
        public int YearsOfService { get; set; }
        public DateTime? JoinDate { get; set; }

        public string RequestedBy { get; set; } = string.Empty;
        public string RequestedByRole { get; set; } = string.Empty;
        public DateTime RequestedDate { get; set; }
        public TransferStatus Status { get; set; }

        // ── Document ──
        public string? DocumentFileName { get; set; }
        public bool HasDocument { get; set; }

        // ── Stage 2: Department Head Review ──
        public string? DeptHeadReview { get; set; }
        public DateTime? DeptHeadReviewDate { get; set; }
        public string? DeptHeadComments { get; set; }

        // ── Stage 3a: Current Branch Manager Review ──
        public string? CurrentBMReview { get; set; }
        public DateTime? CurrentBMReviewDate { get; set; }
        public string? CurrentBMComments { get; set; }

        // ── Stage 3b: Target Branch Manager Review ──
        public string? TargetBMReview { get; set; }
        public DateTime? TargetBMReviewDate { get; set; }
        public string? TargetBMComments { get; set; }

        // ── Stage 4: Area Manager Review ──
        public string? AreaManagerReview { get; set; }
        public DateTime? AreaManagerReviewDate { get; set; }
        public string? AreaManagerComments { get; set; }

        // ── Stage 5: HR Finalization ──
        public string? HRManagerReview { get; set; }
        public DateTime? HRManagerReviewDate { get; set; }
        public string? HRManagerComments { get; set; }

        // ── Managerial Notification Properties ──
        public bool IsManagerialNotification => Status == TransferStatus.PendingHRReview || Status == TransferStatus.ManagerReviewed;

        public string StatusDisplay => Status switch
        {
            TransferStatus.Pending => "Pending Department Head",
            TransferStatus.DeptHeadApproved => "Department Head Approved",
            TransferStatus.DeptHeadRejected => "Rejected by Department Head",
            TransferStatus.CurrentBMApproved => "Approved by Current BM",
            TransferStatus.CurrentBMRejected => "Rejected by Current BM",
            TransferStatus.TargetBMApproved => "Approved by Target BM",
            TransferStatus.TargetBMRejected => "Rejected by Target BM",
            TransferStatus.BothBMsApproved => "Approved by Both BMs",
            TransferStatus.AreaManagerApproved => "Approved by Area Manager",
            TransferStatus.AreaManagerRejected => "Rejected by Area Manager",
            TransferStatus.FullyApproved => "Approved by HR",
            TransferStatus.HRFinalRejected => "Rejected by HR",
            TransferStatus.PendingHRReview => "Pending HR Review (Notice)",
            TransferStatus.ManagerReviewed => "Reviewed by HR Manager (Notice)",
            _ => "Unknown"
        };

        public string StatusBadgeClass => Status switch
        {
            TransferStatus.Pending => "k-badge-pending",
            TransferStatus.DeptHeadApproved => "k-badge-info",
            TransferStatus.DeptHeadRejected => "k-badge-rejected",
            TransferStatus.CurrentBMApproved => "k-badge-info",
            TransferStatus.CurrentBMRejected => "k-badge-rejected",
            TransferStatus.TargetBMApproved => "k-badge-info",
            TransferStatus.TargetBMRejected => "k-badge-rejected",
            TransferStatus.BothBMsApproved => "k-badge-info",
            TransferStatus.AreaManagerApproved => "k-badge-info",
            TransferStatus.AreaManagerRejected => "k-badge-rejected",
            TransferStatus.FullyApproved => "k-badge-approved",
            TransferStatus.HRFinalRejected => "k-badge-rejected",
            TransferStatus.PendingHRReview => "k-badge-pending",
            TransferStatus.ManagerReviewed => "k-badge-approved",
            _ => "k-badge-secondary"
        };
    }

    public enum TransferStatus
    {
        Pending = 0,
        DeptHeadApproved = 1,
        DeptHeadRejected = 2,
        CurrentBMApproved = 3,
        CurrentBMRejected = 4,
        TargetBMApproved = 5,
        TargetBMRejected = 6,
        BothBMsApproved = 7,
        AreaManagerApproved = 8,
        AreaManagerRejected = 9,
        FullyApproved = 10,
        HRFinalRejected = 11,

        // Direct Managerial Notification Workflow (Dept Head, BM, AM)
        PendingHRReview = 12,
        ManagerReviewed = 13
    }
}
