using System;
using System.Collections.Generic;
using HRMS.Domain.Entities.Core;

namespace HRMS.Domain.Entities.Training
{
    public class ProbationPeriod
    {
        public int Id { get; set; }  // PK
        public int EmployeeId { get; set; }  // FK to Employee
        public Employee Employee { get; set; } = null!;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string Status { get; set; } = null!; // Active / Completed / Extended

        // Navigation property
        public ICollection<ProbationFeedback> Feedbacks { get; set; } = new List<ProbationFeedback>();
    }
}
