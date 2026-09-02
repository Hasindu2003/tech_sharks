namespace HRMS.Domain.Entities.Payroll
{
    public class Payslip
    {
        public int Id { get; set; }
        public int PayrollRunId { get; set; }
        public int EmployeeId { get; set; }
        public decimal BasicSalary { get; set; }
        public decimal HousingAllowance { get; set; }
        public decimal TransportAllowance { get; set; }
        public decimal MedicalAllowance { get; set; }
        public decimal Bonuses { get; set; }
        public decimal GrossPay { get; set; }
        public decimal EpfEmployee { get; set; }  // 8% of basic
        public decimal EpfEmployer { get; set; }  // 12% of basic
        public decimal Etf { get; set; }  // 3% of basic
        public decimal TaxDeduction { get; set; }
        public decimal TotalDeductions { get; set; }
        public decimal NetPay { get; set; }
        public string Status { get; set; } = "Draft";

        public PayrollRun? PayrollRun { get; set; }
        public Core.Employee? Employee { get; set; }
    }
}
