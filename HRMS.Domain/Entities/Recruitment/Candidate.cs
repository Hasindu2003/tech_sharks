using System;

namespace HRMS.Domain.Entities.Recruitment
{
    public class Candidate
    {
        public int Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? ContactNo { get; set; }
        public string? CVPath { get; set; } // CV save path
        public string? Skills { get; set; }
        public int ExperienceYears { get; set; }
        public double Score { get; set; } // score given by AI
        public string Status { get; set; } = "Pending";
        public DateTime AppliedDate { get; set; } = DateTime.Now;
    }
}