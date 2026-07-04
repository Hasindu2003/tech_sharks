using HRMS.Domain.Entities.Leave;

namespace HRMS.Application.Leave
{
    public interface ILeaveService
    {
        Task<List<LeaveBalanceDto>> GetBalancesAsync(int employeeId, int year);
        Task<List<LeaveSummaryDto>> GetMyLeavesAsync(int employeeId);
        Task<LeaveDetailsDto?> GetDetailsAsync(int leaveId);

        Task<LeaveOperationResult<LeaveSummaryDto>> ApplyAsync(ApplyLeaveRequest request);
        Task<LeaveOperationResult<LeaveSummaryDto>> CancelAsync(int leaveId, int employeeId, string reason);

        Task<List<LeaveSummaryDto>> GetPendingForManagerAsync(int managerEmployeeId);
        Task<List<LeaveSummaryDto>> GetPendingForHrAsync();

        Task<LeaveOperationResult<LeaveSummaryDto>> ManagerActionAsync(
            int leaveId, int managerEmployeeId, ApprovalAction action, string? comments);

        Task<LeaveOperationResult<LeaveSummaryDto>> HrActionAsync(
            int leaveId, int hrEmployeeId, ApprovalAction action, string? comments);

        Task<LeaveOperationResult<LeaveBalanceDto>> AdjustBalanceAsync(
            int employeeId, LeaveType leaveType, int year, decimal deltaDays, string reason, int adjustedByEmployeeId);
    }
}
