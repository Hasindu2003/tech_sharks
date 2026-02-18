using System;
using HRMS.Domain.Entities.Core;

namespace HRMS.Domain.Entities.Training
{
    public class TrainingProgramRequest
    {
        public int Id { get; set; }  // PK
        public int EmployeeId { get; set; }  // FK
        public Employee Employee { get; set; } = null!;
        public string Title { get; set; } = null!;
        public string? Description { get; set; }
        public DateTime RequestedDate { get; set; }
        public string Status { get; set; } = null!;  // Pending / Approved / Rejected
    }
}
