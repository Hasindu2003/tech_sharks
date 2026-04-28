using System.ComponentModel.DataAnnotations;

namespace HRMS.Domain.Entities.Transfer
{
    public class TransferRequest
    {
        [Key]
        public int Id { get; set; }

        // ── Employee Details (auto-filled at request time) ──
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
        public string CurrentBranch { get; set; } = string.Empty;

        [Required]
        [MaxLength(200)]
        public string CurrentDesignation { get; set; } = string.Empty;

        [MaxLength(100)]
        public string Department { get; set; } = string.Empty;

        // ── Transfer Details ──
        [Required]
        [MaxLength(200)]
        public string RequestedBranch { get; set; } = string.Empty;

        [Required]
        [MaxLength(500)]
        public string Reason { get; set; } = string.Empty;

        public DateTime? PreferredDate { get; set; }

        public int YearsOfService { get; set; }

        [Required]
        [MaxLength(256)]
        public string RequestedBy { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string RequestedByRole { get; set; } = string.Empty;

        public DateTime RequestedDate { get; set; }

        public TransferRequestStatus Status { get; set; }

        // ── Document Upload (stored in DB) ──
        public byte[]? DocumentData { get; set; }

        [MaxLength(256)]
        public string? DocumentFileName { get; set; }

        [MaxLength(100)]
        public string? DocumentContentType { get; set; }

        // ── HR Manager Review (Stage 1) ──
        [MaxLength(50)]
        public string? HRManagerReview { get; set; }
        public DateTime? HRManagerReviewDate { get; set; }
        [MaxLength(1000)]
        public string? HRManagerComments { get; set; }

        // ── Current Branch Manager Review (Stage 2a) ──
        [MaxLength(50)]
        public string? CurrentBMReview { get; set; }
        public DateTime? CurrentBMReviewDate { get; set; }
        [MaxLength(1000)]
        public string? CurrentBMComments { get; set; }

        // ── Target Branch Manager Review (Stage 2b) ──
        [MaxLength(50)]
        public string? TargetBMReview { get; set; }
        public DateTime? TargetBMReviewDate { get; set; }
        [MaxLength(1000)]
        public string? TargetBMComments { get; set; }

        // ── Area Manager Review (Stage 3) ──
        [MaxLength(50)]
        public string? AreaManagerReview { get; set; }
        public DateTime? AreaManagerReviewDate { get; set; }
        [MaxLength(1000)]
        public string? AreaManagerComments { get; set; }
    }

    public enum TransferRequestStatus
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