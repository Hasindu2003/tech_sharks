using System;
using System.Collections.Generic;
using HRMS.Domain.Entities.Core;

namespace HRMS.Domain.Entities.Training
{
    public class Training
    {
        public int Id { get; set; }  // PK
        public string Title { get; set; } = null!;         // Name of the training
        public string Description { get; set; } = null!;   // Description
        public DateTime Date { get; set; }                 // Training day
        public TimeSpan StartTime { get; set; }            // Start time
        public int DurationHours { get; set; }             // Duration in hours
        public int? TrainerId { get; set; }                // FK to Trainer
        public Trainer? Trainer { get; set; }

        public string Status { get; set; } = null!;       // Scheduled / Completed / Cancelled

        // Navigation properties
        public ICollection<EmployeeTraining> EmployeeTrainings { get; set; } = new List<EmployeeTraining>();
        public ICollection<TrainingFeedback> Feedbacks { get; set; } = new List<TrainingFeedback>();
    }
}
