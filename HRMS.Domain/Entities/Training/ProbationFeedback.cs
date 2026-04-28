using System;
using HRMS.Domain.Entities.Core;

namespace HRMS.Domain.Entities.Training
{
    public class ProbationFeedback
    {
        public int Id { get; set; }  // PK
        public int ProbationPeriodId { get; set; }  // FK → ProbationPeriod
        public ProbationPeriod ProbationPeriod { get; set; } = null!;
        public int SupervisorId { get; set; }  // FK → Employee
        public Employee Supervisor { get; set; } = null!;
        public int Rating { get; set; }  // 1–5
        public string? Comments { get; set; }
        public DateTime SubmissionDate { get; set; }
    }
}
