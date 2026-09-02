using System;

namespace HRMS.Domain.Entities.Payroll
{
    public class PayrollPolicySetting
    {
        public int Id { get; set; }
        public int? BranchId { get; set; }
        public int StandardMonthlyWorkingDays { get; set; } = 21;
        public decimal StandardDailyWorkingHours { get; set; } = 8.0m;
        public decimal StandardOtMultiplier { get; set; } = 1.5m;
        public decimal WeekendOtMultiplier { get; set; } = 2.0m;
        public bool AutoCalculateOtOnPayroll { get; set; } = true;
        public DateTime LastModifiedDate { get; set; } = DateTime.Now;
        public string? ModifiedBy { get; set; }

        public Core.Branch? Branch { get; set; }
    }
}
