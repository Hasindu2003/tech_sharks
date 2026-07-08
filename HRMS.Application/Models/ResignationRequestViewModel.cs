namespace HRMS.Application.Models
{
    public class ResignationRequestViewModel
    {
        public int Id { get; set; }

        public string EmployeeName { get; set; } = string.Empty;
        public string EpfNumber { get; set; } = string.Empty;
        public string EmployeeEmail { get; set; } = string.Empty;
        public string Branch { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public string Designation { get; set; } = string.Empty;

        public string ReasonForResignation { get; set; } = string.Empty;
        public DateTime ResignationDate { get; set; }
        public DateTime EffectiveDate { get; set; }
        public int NoticePeriodDays { get; set; }
        public string? AdditionalRemarks { get; set; }

        public bool HasOutstandingLoans { get; set; }
        public bool IsLoanGuarantor { get; set; }
        public bool HasOverridePermission { get; set; }
        public string? ObligationDetails { get; set; }

        public ResignationStatusEnum Status { get; set; }
        public string InitiatedBy { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; }
        public DateTime LastModifiedDate { get; set; }

        public string? BMReview { get; set; }
        public DateTime? BMReviewDate { get; set; }
        public string? BMComments { get; set; }
        public string? BMEmail { get; set; }

        public string? AMReview { get; set; }
        public DateTime? AMReviewDate { get; set; }
        public string? AMComments { get; set; }
        public string? AMEmail { get; set; }

        public string? HRReview { get; set; }
        public DateTime? HRReviewDate { get; set; }
        public string? HRComments { get; set; }
        public string? HREmail { get; set; }

        public bool AcceptanceLetterGenerated { get; set; }
        public DateTime? AcceptanceLetterDate { get; set; }
        public bool AccountDeactivated { get; set; }
        public DateTime? AccountDeactivatedDate { get; set; }
        public string? AccountDeactivatedBy { get; set; }

        public List<ResignationDocumentViewModel> Documents { get; set; } = new();
        public int DocumentCount { get; set; }

        public string StatusDisplay => Status switch
        {
            ResignationStatusEnum.Draft => "Draft",
            ResignationStatusEnum.SubmittedForApproval => "Submitted for Approval",
            ResignationStatusEnum.BMApproved => "Acknowledged by Branch Manager",
            ResignationStatusEnum.BMRejected => "Rejected by Branch Manager",
            ResignationStatusEnum.AMApproved => "Approved by Area Manager",
            ResignationStatusEnum.AMRejected => "Rejected by Area Manager",
            ResignationStatusEnum.HRApproved => "Approved by HR Manager",
            ResignationStatusEnum.HRRejected => "Rejected by HR Manager",
            ResignationStatusEnum.Completed => "Completed",
            _ => "Unknown"
        };

        public string StatusBadgeClass => Status switch
        {
            ResignationStatusEnum.Draft => "k-badge-info",
            ResignationStatusEnum.SubmittedForApproval => "k-badge-pending",
            ResignationStatusEnum.BMApproved => "k-badge-pending",
            ResignationStatusEnum.BMRejected => "k-badge-rejected",
            ResignationStatusEnum.AMApproved => "k-badge-pending",
            ResignationStatusEnum.AMRejected => "k-badge-rejected",
            ResignationStatusEnum.HRApproved => "k-badge-approved",
            ResignationStatusEnum.HRRejected => "k-badge-rejected",
            ResignationStatusEnum.Completed => "k-badge-terminated",
            _ => "k-badge-secondary"
        };

        public bool IsEffectiveDateReached => DateTime.Today >= EffectiveDate.Date;
        public bool CanProcessDeactivation => Status == ResignationStatusEnum.HRApproved
                                              && !AccountDeactivated
                                              && IsEffectiveDateReached;
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
        SubmittedForApproval = 1,
        BMApproved = 2,
        BMRejected = 3,
        AMApproved = 4,
        AMRejected = 5,
        HRApproved = 6,
        HRRejected = 7,
        Completed = 8
    }
}
