using HRMS.Domain.Entities.Leave;

namespace HRMS.Application.Payroll
{
    public class PayrollLeaveTypeSummaryDto
    {
        public LeaveType LeaveType { get; set; }
        public string LeaveTypeName { get; set; } = null!;
        public bool IsPaid { get; set; }
        public decimal Days { get; set; }
    }

    public class PayrollLeaveSummaryDto
    {
        public int EmployeeId { get; set; }
        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }
        public decimal TotalPaidLeaveDays { get; set; }
        public decimal TotalUnpaidLeaveDays { get; set; }
        public List<PayrollLeaveTypeSummaryDto> ByType { get; set; } = new();
    }
}
