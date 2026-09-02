using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using HRMS.Domain.Entities.Core;

namespace HRMS.Domain.Entities.Recruitment
{
    public class JobOpening
    {
        [Key]
        public int Id { get; set; }

        [MaxLength(50)]
        public string? JobCode { get; set; } // e.g. "JOB-2026-001" (Auto-generated on creation)

        [Required]
        [MaxLength(150)]
        public string Title { get; set; } = string.Empty;

        [MaxLength(2000)]
        public string? Description { get; set; }

        [MaxLength(2000)]
        public string? Requirements { get; set; }

        public int? DepartmentId { get; set; }
        [ForeignKey("DepartmentId")]
        public virtual Department? Department { get; set; }

        public int? BranchId { get; set; }
        [ForeignKey("BranchId")]
        public virtual Branch? Branch { get; set; }

        [MaxLength(50)]
        public string EmploymentType { get; set; } = "Full-Time"; // Full-Time, Part-Time, Contract, Internship

        public int MinimumExperienceYears { get; set; } = 0;

        [MaxLength(50)]
        public string MinimumEducationLevel { get; set; } = "None"; // None, Degree, Masters

        [MaxLength(500)]
        public string? RequiredSkills { get; set; } // Comma-separated required skills

        [Required]
        [MaxLength(30)]
        public string Status { get; set; } = "Open"; // Open, Closed, Draft

        public DateTime CreatedDate { get; set; } = DateTime.Now;

        public DateTime? ClosingDate { get; set; }

        [MaxLength(100)]
        public string? CreatedByUserId { get; set; }

        public virtual ICollection<CVBank> Applications { get; set; } = new List<CVBank>();
    }
}
