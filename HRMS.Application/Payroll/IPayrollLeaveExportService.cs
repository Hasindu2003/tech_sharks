namespace HRMS.Application.Payroll
{
    // Read-only query surface consumed by a future Payroll module (none exists in this repo yet).
    public interface IPayrollLeaveExportService
    {
        Task<PayrollLeaveSummaryDto> GetPayrollLeaveSummaryAsync(int employeeId, DateTime periodStart,
            DateTime periodEnd);
    }
}
