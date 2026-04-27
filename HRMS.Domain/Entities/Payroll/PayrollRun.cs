namespace HRMS.Domain.Entities.Payroll
{
    public class PayrollRun
    {
        public int Id { get; set; }
        public int Month { get; set; }
        public int Year { get; set; }
        public string Status { get; set; } = "Draft"; // Draft, Processing, Completed
        public DateTime? ProcessedAt { get; set; }
        public decimal TotalAmount { get; set; }
        public int TotalEmployees { get; set; }

        public List<Payslip> Payslips { get; set; } = new();

        public string MonthName => new DateTime(Year, Month, 1).ToString("MMMM yyyy");
    }
}
