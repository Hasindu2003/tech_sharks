using System;
using HRMS.Domain.Entities.Core;

namespace HRMS.Domain.Entities.Leave
{
    public class MaternityPayment
    {
        public int Id { get; set; }

        public int LeaveId { get; set; }
        public Leave Leave { get; set; } = null!;

        public string SalaryAdjustmentType { get; set; } = "Full";
        public decimal SalaryPercentage { get; set; } = 100;
        public decimal PaymentAmount { get; set; }
        public DateTime? PaymentDate { get; set; }
        public string Status { get; set; } = "Pending";
        public string? NursingBreakConfig { get; set; }
        public string? ProcessedBy { get; set; }
    }
}
