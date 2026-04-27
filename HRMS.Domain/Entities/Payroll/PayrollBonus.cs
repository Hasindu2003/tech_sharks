namespace HRMS.Domain.Entities.Payroll
{
    public class PayrollBonus
    {
        public int Id { get; set; }
        public int EmployeeId { get; set; }
        public string BonusType { get; set; } = string.Empty; // Performance, Overtime, Festival, Other
        public decimal Amount { get; set; }
        public int Month { get; set; }
        public int Year { get; set; }
        public string? Reason { get; set; }

        public Core.Employee? Employee { get; set; }
    }
}
