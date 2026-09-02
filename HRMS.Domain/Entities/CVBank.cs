using System;
using System.ComponentModel.DataAnnotations;

namespace HRMS.Domain.Entities
{
    public class CVBank
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string CandidateName { get; set; } = null!;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = null!;

        public string? ContactNumber { get; set; }

        [Required]
        public string AppliedPosition { get; set; } = null!;

        public int ExperienceYears { get; set; }

        public string? Skills { get; set; }

        public string? CVFilePath { get; set; }

        public DateTime UploadedDate { get; set; } = DateTime.Now;

        public bool HasDegree { get; set; }
        public bool HasMasters { get; set; }
    
        public int ExperienceScore { get; set; }

        public int? JobOpeningId { get; set; }
        public virtual Recruitment.JobOpening? JobOpening { get; set; }
    }
}
