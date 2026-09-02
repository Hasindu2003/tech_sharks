using System;
using HRMS.Domain.Entities.Core;

namespace HRMS.Domain.Entities.Leave
{
    public class LeaveEntitlement
    {
        public int Id { get; set; }

        public int EmployeeId { get; set; }
        public Employee Employee { get; set; } = null!;

        public string LeaveType { get; set; } = null!;
        public double TotalDays { get; set; }
        public double UsedDays { get; set; }
        public double RemainingDays { get; set; }

        public int Year { get; set; }
    }
}
