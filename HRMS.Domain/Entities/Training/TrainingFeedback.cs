using System;
using HRMS.Domain.Entities.Core;

namespace HRMS.Domain.Entities.Training
{
    public class TrainingFeedback
    {
        public int Id { get; set; }  // PK
        public int EmployeeId { get; set; }  // FK
        public Employee Employee { get; set; } = null!;
        public int TrainingId { get; set; }  // FK
        public Training Training { get; set; } = null!;
        public int Rating { get; set; }  // 1–5
        public string? Comments { get; set; }
        public DateTime SubmissionDate { get; set; }
    }
}
