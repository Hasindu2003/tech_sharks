using System.ComponentModel.DataAnnotations.Schema;
using HRMS.Domain.Entities.Core;

namespace HRMS.Domain.Entities.Leave
{
    // The yearly leave balance ledger for one employee/leave type/year.
    public class LeaveEntitlement
    {
        public int Id { get; set; }

        public int EmployeeId { get; set; }
        public Employee Employee { get; set; } = null!;

        public LeaveType LeaveType { get; set; }
        public int Year { get; set; }

        public decimal AllocatedDays { get; set; }
        public decimal CarriedForwardDays { get; set; }
        public decimal UsedDays { get; set; }

        [NotMapped] public decimal RemainingDays => AllocatedDays + CarriedForwardDays - UsedDays;
    }
}
