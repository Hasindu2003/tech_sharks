using System;
using HRMS.Domain.Entities.Core;

namespace HRMS.Domain.Entities.Leave
{
    public class MaternityLeave
    {
        public int Id { get; set; }  // Primary Key

        public int LeaveId { get; set; }       // FK to Leave
        public Leave Leave { get; set; } = null!;

        public string? MedicalCertificate { get; set; }
        public DateTime? ExpectedDeliveryDate { get; set; }
    }
}
