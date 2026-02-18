using HRMS.Domain.Entities.Core;
using System.Collections.Generic;

namespace HRMS.Domain.Entities.Training
{
    public class Trainer
    {
        public int Id { get; set; }  // PK
        public int? EmployeeId { get; set; }  // FK if internal
        public Employee? Employee { get; set; }
        public string Name { get; set; } = null!;  // For external trainers
        public string Expertise { get; set; } = null!;
        public int? DepartmentId { get; set; }  // Optional

        public ICollection<Training> Trainings { get; set; } = new List<Training>();
    }
}
