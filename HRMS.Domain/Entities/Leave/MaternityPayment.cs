using System;
using HRMS.Domain.Entities.Core;

namespace HRMS.Domain.Entities.Leave
{
    public class MaternityPayment
    {
        public int Id { get; set; }  // Primary Key

        public int LeaveId { get; set; }       // FK to Leave
        public Leave Leave { get; set; } = null!;

        public decimal PaymentAmount { get; set; }
        public decimal SalaryPercentage { get; set; }  // Percentage of salary paid during leave
        public DateTime PaymentDate { get; set; }
        public string Status { get; set; } = null!;  // Paid / Pending
    }
}
