using System;
using HRMS.Domain.Entities.Core;

namespace HRMS.Domain.Entities.Leave
{
    public class OverseasLeave
    {
        public int Id { get; set; }  // Primary Key

        public int LeaveId { get; set; }       // FK to Leave
        public Leave Leave { get; set; } = null!;

        public string PassportNumber { get; set; } = null!;
        public DateTime PassportExpiry { get; set; }
        public string Country { get; set; } = null!;
        public string? ContactDetailsOverseas { get; set; }
        public string? Purpose { get; set; }
        public string? TravelDocuments { get; set; }
        public string? PassportCopyPath { get; set; }
        public string? ConfirmationLetterPath { get; set; }

        public string VerificationStatus { get; set; } = "New";
        public string? VerificationComments { get; set; }
        public string BoardApprovalStatus { get; set; } = "Pending";
        public string? BoardRejectionReason { get; set; }
    }
}
