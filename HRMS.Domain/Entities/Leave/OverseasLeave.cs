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
        public string? TravelDocuments { get; set; }  // Optional: visa, tickets
        public string? Purpose { get; set; }          // Optional description
    }
}
