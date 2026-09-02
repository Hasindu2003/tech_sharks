using System.ComponentModel.DataAnnotations;

namespace HRMS.Domain.Entities.Transfer
{
    public class TransferRequest
    {
        [Key]
        public int Id { get; set; }

        // ── Employee Details (auto-filled at request time) ──
        [Required, MaxLength(100)]
        public string EmployeeName { get; set; } = string.Empty;

        [Required, MaxLength(20)]
        public string EpfNumber { get; set; } = string.Empty;

        [Required, MaxLength(256)]
        public string EmployeeEmail { get; set; } = string.Empty;

        [Required, MaxLength(200)]
        public string CurrentBranch { get; set; } = string.Empty;

        [Required, MaxLength(200)]
        public string CurrentDesignation { get; set; } = string.Empty;

        [MaxLength(100)]
        public string Department { get; set; } = string.Empty;

        // ── Transfer Details ──
        [Required, MaxLength(200)]
        public string RequestedBranch { get; set; } = string.Empty;

        [Required, MaxLength(500)]
        public string Reason { get; set; } = string.Empty;

        public DateTime? PreferredDate { get; set; }
        public int YearsOfService { get; set; }
        public DateTime? JoinDate { get; set; }

        [Required, MaxLength(256)]
        public string RequestedBy { get; set; } = string.Empty;

        [Required, MaxLength(50)]
        public string RequestedByRole { get; set; } = string.Empty;

        public DateTime RequestedDate { get; set; }
        public TransferRequestStatus Status { get; set; }

        // ── Document Upload ──
        public byte[]? DocumentData { get; set; }

        [MaxLength(256)]
        public string? DocumentFileName { get; set; }

        [MaxLength(100)]
        public string? DocumentContentType { get; set; }

        // ── Stage 2: Department Head Review ──
        [MaxLength(50)]
        public string? DeptHeadReview { get; set; }
        public DateTime? DeptHeadReviewDate { get; set; }
        [MaxLength(1000)]
        public string? DeptHeadComments { get; set; }

        // ── Stage 3a: Current Branch Manager Review ──
        [MaxLength(50)]
        public string? CurrentBMReview { get; set; }
        public DateTime? CurrentBMReviewDate { get; set; }
        [MaxLength(1000)]
        public string? CurrentBMComments { get; set; }

        // ── Stage 3b: Target Branch Manager Review ──
        [MaxLength(50)]
        public string? TargetBMReview { get; set; }
        public DateTime? TargetBMReviewDate { get; set; }
        [MaxLength(1000)]
        public string? TargetBMComments { get; set; }

        // ── Stage 4: Area Manager Review ──
        [MaxLength(50)]
        public string? AreaManagerReview { get; set; }
        public DateTime? AreaManagerReviewDate { get; set; }
        [MaxLength(1000)]
        public string? AreaManagerComments { get; set; }

        // ── Stage 5: HR Finalization ──
        [MaxLength(50)]
        public string? HRManagerReview { get; set; }
        public DateTime? HRManagerReviewDate { get; set; }
        [MaxLength(1000)]
        public string? HRManagerComments { get; set; }
    }

    public enum TransferRequestStatus
    {
        // Stage 1: HR Officer submitted, awaiting Department Head
        Pending = 0,

        // Stage 2: Department Head decision
        DeptHeadApproved = 1,
        DeptHeadRejected = 2,

        // Stage 3: Branch Managers (parallel)
        CurrentBMApproved = 3,
        CurrentBMRejected = 4,
        TargetBMApproved = 5,
        TargetBMRejected = 6,
        BothBMsApproved = 7,

        // Stage 4: Area Manager decision
        AreaManagerApproved = 8,
        AreaManagerRejected = 9,

        // Stage 5: HR Finalization
        FullyApproved = 10,
        HRFinalRejected = 11,

        // Direct Managerial Notification Workflow (Dept Head, BM, AM)
        PendingHRReview = 12,
        ManagerReviewed = 13
    }
}
