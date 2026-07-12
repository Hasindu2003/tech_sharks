using System;
using HRMS.Domain.Entities.Core;

namespace HRMS.Domain.Entities.Leave
{
    public class MaternityLeave
    {
        public int Id { get; set; }  // Primary Key

        public int LeaveId { get; set; }       // FK to Leave
        public Leave Leave { get; set; } = null!;

        public int LeaveLevel { get; set; } = 1;
        public int ChildNumber { get; set; } = 1;
        public DateTime? ExpectedDeliveryDate { get; set; }
        public string? MedicalCertificate { get; set; }
        public string? MedicalCertificatePath { get; set; }
        public string? DoctorLetterPath { get; set; }
        public string VerificationStatus { get; set; } = "Pending";
        public string? VerificationComments { get; set; }
    }
}
