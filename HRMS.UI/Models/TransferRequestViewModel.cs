using System.ComponentModel.DataAnnotations;
using HRMS.UI.Validation;

namespace HRMS.UI.Models
{
    public class TransferRequestViewModel
    {
        public int Id { get; set; }

        // ── Auto-filled Employee Details ──
        [Display(Name = "Employee Name")]
        public string EmployeeName { get; set; } = string.Empty;

        [Display(Name = "EPF Number")]
        public string EpfNumber { get; set; } = string.Empty;

        [Display(Name = "Employee Email")]
        public string EmployeeEmail { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Current Branch")]
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
        [StringLength(500, MinimumLength = 20,
            ErrorMessage = "Reason must be between 20 and 500 characters.")]
        public string Reason { get; set; } = string.Empty;

        [Display(Name = "Preferred Transfer Date")]
        [DataType(DataType.Date)]
        [FutureDate(MinimumDaysAhead = 7,
            ErrorMessage = "Preferred date must be at least 7 days from today and no more than 1 year ahead.")]
        public DateTime? PreferredDate { get; set; }

        [Display(Name = "Years of Service")]
        [Range(0, 50, ErrorMessage = "Years of service must be between 0 and 50.")]
        public int YearsOfService { get; set; }

        public string RequestedBy { get; set; } = string.Empty;
        public string RequestedByRole { get; set; } = string.Empty;
        public DateTime RequestedDate { get; set; }
        public TransferStatus Status { get; set; }

        // ── Document ──
        public string? DocumentFileName { get; set; }
        public bool HasDocument { get; set; }

        // ── HR Manager Review (Stage 1) ──
        public string? HRManagerReview { get; set; }
        public DateTime? HRManagerReviewDate { get; set; }
        public string? HRManagerComments { get; set; }

        // ── Current Branch Manager Review (Stage 2a) ──
        public string? CurrentBMReview { get; set; }
        public DateTime? CurrentBMReviewDate { get; set; }
        public string? CurrentBMComments { get; set; }

        // ── Target Branch Manager Review (Stage 2b) ──
        public string? TargetBMReview { get; set; }
        public DateTime? TargetBMReviewDate { get; set; }
        public string? TargetBMComments { get; set; }

        // ── Area Manager Review (Stage 3) ──
        public string? AreaManagerReview { get; set; }
        public DateTime? AreaManagerReviewDate { get; set; }
        public string? AreaManagerComments { get; set; }

        // ── Legacy compatibility (kept for old references) ──
        public string? BranchManagerReview => CurrentBMReview;
        public DateTime? BranchManagerReviewDate => CurrentBMReviewDate;
        public string? BranchManagerComments => CurrentBMComments;
    }

    public enum TransferStatus
    {
        Pending = 0,
        HRManagerApproved = 1,
        HRManagerRejected = 2,
        CurrentBMApproved = 3,
        CurrentBMRejected = 4,
        TargetBMApproved = 5,
        TargetBMRejected = 6,
        BothBMsApproved = 7,
        AreaManagerApproved = 8,
        AreaManagerRejected = 9
    }
}