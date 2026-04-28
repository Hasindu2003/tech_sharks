using System;
using System.Collections.Generic;
using HRMS.Domain.Entities.Core;

namespace HRMS.Domain.Entities.Training
{
    public class InternProgram
    {
        public int Id { get; set; }  // PK
        public int EmployeeId { get; set; }  // FK to Employee
        public Employee Employee { get; set; } = null!;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int SupervisorId { get; set; }  // FK to Employee (department head)
        public Employee Supervisor { get; set; } = null!;
        public int DepartmentId { get; set; }  // FK to Department

        // Navigation property
        public ICollection<InternFeedback> Feedbacks { get; set; } = new List<InternFeedback>();
    }
}
