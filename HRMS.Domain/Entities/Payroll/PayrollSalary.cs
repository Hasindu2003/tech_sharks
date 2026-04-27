namespace HRMS.Domain.Entities.Payroll
{
    public class PayrollSalary
    {
        public int Id { get; set; }
        public int EmployeeId { get; set; }
        public decimal BasicSalary { get; set; }
        public decimal HousingAllowance { get; set; }
        public decimal TransportAllowance { get; set; }
        public decimal MedicalAllowance { get; set; }
        public DateTime EffectiveDate { get; set; }

        public Core.Employee? Employee { get; set; }
    }
}
