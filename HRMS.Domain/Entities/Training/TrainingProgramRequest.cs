using System;
using HRMS.Domain.Entities.Core;

namespace HRMS.Domain.Entities.Training
{
    public class TrainingProgramRequest
    {
        public int Id { get; set; }  // Primary Key
        public int EmployeeId { get; set; }  // Foreign Key
        public virtual Employee Employee { get; set; } = null!;
        public string Title { get; set; } = null!;
        public string? Description { get; set; }
        public DateTime RequestedDate { get; set; } = DateTime.Now;
        public string Status { get; set; } = "Pending";  // Pending / Approved / Rejected
        
        // අනුමත කළ පසු වැඩසටහන පවත්වන දිනය
        public DateTime? ScheduledDate { get; set; }
    }
}