using System;
using HRMS.Domain.Entities.Core;  // Reference to Employee

namespace HRMS.Domain.Entities.Attendance
{
    public class Attendance
    {
        public int Id { get; set; }

        public int EmployeeId { get; set; }
        public Employee Employee { get; set; } = null!;

        public DateTime Date { get; set; }
        public DateTime? TimeIn { get; set; }
        public DateTime? TimeOut { get; set; }

        public string Status { get; set; } = null!; //present, absent, late, on leave, etc.
    }
}
