namespace HRMS.Application.Models
{
    public class ResignationRequestViewModel
    {
        public int Id { get; set; }

        // ── Employee Details ──
        public string EmployeeName { get; set; } = string.Empty;
        public string EpfNumber { get; set; } = string.Empty;
        public string EmployeeEmail { get; set; } = string.Empty;
        public string Branch { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public string Designation { get; set; } = string.Empty;

        // ── Resignation Details ──
        public string ReasonForResignation { get; set; } = string.Empty;
        public DateTime ResignationDate { get; set; }
        public DateTime EffectiveDate { get; set; }
        public int NoticePeriodDays { get; set; }
        public string? AdditionalRemarks { get; set; }

        // ── Obligations ──
        public bool HasOutstandingLoans { get; set; }
        public bool IsLoanGuarantor { get; set; }
        public bool HasOverridePermission { get; set; }
        public string? ObligationDetails { get; set; }

        // ── Workflow ──
        public ResignationStatusEnum Status { get; set; }
        public string InitiatedBy { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; }
        public DateTime LastModifiedDate { get; set; }

        // ── Stage 1: Branch Manager ──
        public string? BMReview { get; set; }
        public DateTime? BMReviewDate { get; set; }
        public string? BMComments { get; set; }
        public string? BMEmail { get; set; }

        // ── Stage 2: Area Manager ──
        public string? AMReview { get; set; }
        public DateTime? AMReviewDate { get; set; }
        public string? AMComments { get; set; }
        public string? AMEmail { get; set; }

        // ── Stage 3: HR Manager ──
        public string? HRReview { get; set; }
        public DateTime? HRReviewDate { get; set; }
        public string? HRComments { get; set; }
        public string? HREmail { get; set; }

        // ── Post-Approval ──
        public bool AcceptanceLetterGenerated { get; set; }
        public DateTime? AcceptanceLetterDate { get; set; }
        public bool AccountDeactivated { get; set; }
        public DateTime? AccountDeactivatedDate { get; set; }
        public string? AccountDeactivatedBy { get; set; }

        // ── Department Reviews (Stage 2: All Department Heads in Branch) ──
        public List<ResignationDepartmentReviewViewModel> DepartmentReviews { get; set; } = new();
        public int TotalDeptHeadsCount => DepartmentReviews.Count;
        public int DeptHeadsApprovedCount => DepartmentReviews.Count(d => d.Status == "Approved");
        public int DeptHeadsRejectedCount => DepartmentReviews.Count(d => d.Status == "Rejected");
        public int DeptHeadsPendingCount => DepartmentReviews.Count(d => d.Status == "Pending");
        public bool AreAllDeptHeadsApproved => TotalDeptHeadsCount > 0 && DeptHeadsApprovedCount == TotalDeptHeadsCount;

        // ── Documents ──
        public List<ResignationDocumentViewModel> Documents { get; set; } = new();
        public int DocumentCount { get; set; }

        // ── Helpers ──
        public bool IsManagerialNotification => Status == ResignationStatusEnum.PendingHRReview || Status == ResignationStatusEnum.ManagerReviewed;

        public string StatusDisplay => Status switch
        {
            ResignationStatusEnum.Draft => "Draft",
            ResignationStatusEnum.SubmittedForApproval => TotalDeptHeadsCount > 0
                ? $"Pending Dept Heads ({DeptHeadsApprovedCount}/{TotalDeptHeadsCount})"
                : "Pending Dept Heads",
            ResignationStatusEnum.DeptHeadRejected => "Rejected by Department Head",
            ResignationStatusEnum.DeptHeadsApproved => "Dept Heads Approved – Awaiting Branch Manager",
            ResignationStatusEnum.BMApproved => "Acknowledged by Branch Manager",
            ResignationStatusEnum.BMRejected => "Rejected by Branch Manager",
            ResignationStatusEnum.AMApproved => "Approved by Area Manager",
            ResignationStatusEnum.AMRejected => "Rejected by Area Manager",
            ResignationStatusEnum.HRApproved => "Approved by HR Officer",
            ResignationStatusEnum.HRRejected => "Rejected by HR Officer",
            ResignationStatusEnum.Completed => "Completed",
            ResignationStatusEnum.PendingHRReview => "Pending HR Review (Notice)",
            ResignationStatusEnum.ManagerReviewed => "Reviewed by HR Manager (Notice)",
            _ => "Unknown"
        };

        public string StatusBadgeClass => Status switch
        {
            ResignationStatusEnum.Draft => "k-badge-info",
            ResignationStatusEnum.SubmittedForApproval => "k-badge-pending",
            ResignationStatusEnum.DeptHeadRejected => "k-badge-rejected",
            ResignationStatusEnum.DeptHeadsApproved => "k-badge-info",
            ResignationStatusEnum.BMApproved => "k-badge-pending",
            ResignationStatusEnum.BMRejected => "k-badge-rejected",
            ResignationStatusEnum.AMApproved => "k-badge-pending",
            ResignationStatusEnum.AMRejected => "k-badge-rejected",
            ResignationStatusEnum.HRApproved => "k-badge-approved",
            ResignationStatusEnum.HRRejected => "k-badge-rejected",
            ResignationStatusEnum.Completed => "k-badge-terminated",
            ResignationStatusEnum.PendingHRReview => "k-badge-pending",
            ResignationStatusEnum.ManagerReviewed => "k-badge-approved",
            _ => "k-badge-secondary"
        };

        public bool IsEffectiveDateReached => DateTime.Today >= EffectiveDate.Date;
        public bool CanProcessDeactivation => Status == ResignationStatusEnum.HRApproved
                                              && !AccountDeactivated
                                              && IsEffectiveDateReached;
    }

    public class ResignationDepartmentReviewViewModel
    {
        public int Id { get; set; }
        public int ResignationRequestId { get; set; }
        public int? DepartmentId { get; set; }
        public string DepartmentName { get; set; } = string.Empty;
        public string? ReviewerUserId { get; set; }
        public string? ReviewerName { get; set; }
        public string? ReviewerEmail { get; set; }
        public string Status { get; set; } = "Pending";
        public string? Comments { get; set; }
        public DateTime? ReviewDate { get; set; }
    }

    public class ResignationDocumentViewModel
    {
        public int Id { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public DateTime UploadedDate { get; set; }
    }

    public enum ResignationStatusEnum
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
        Completed = 10,            // Account Deactivated

        // Direct Managerial Notification Workflow (Dept Head, BM, AM)
        PendingHRReview = 11,
        ManagerReviewed = 12
    }
}
