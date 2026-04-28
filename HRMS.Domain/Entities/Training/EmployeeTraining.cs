using HRMS.Domain.Entities.Core;

namespace HRMS.Domain.Entities.Training
{
    public class EmployeeTraining
    {
        public int Id { get; set; }  // PK
        public int EmployeeId { get; set; }  // FK
        public Employee Employee { get; set; } = null!;
        public int TrainingId { get; set; }  // FK
        public Training Training { get; set; } = null!;
        public string AttendanceStatus { get; set; } = null!; // Attended / Absent / Excused
        public string? Score { get; set; }  // Optional rating/score
    }
}
