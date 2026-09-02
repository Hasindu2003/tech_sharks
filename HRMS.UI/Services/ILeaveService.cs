using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HRMS.Domain.Entities.Leave;

namespace HRMS.UI.Services
{
    public interface ILeaveService
    {
        Task<LeaveEntitlement> GetLeaveBalanceAsync(int employeeId, string leaveType, int year);
        Task<List<LeaveEntitlement>> GetAllLeaveBalancesAsync(int employeeId, int year);
        Task<Leave> ApplyLeaveAsync(Leave leave);
        Task<List<Leave>> GetEmployeeLeavesAsync(int employeeId);
        Task<List<Leave>> GetPendingApprovalsAsync(int approverId);
        Task<Leave> ApproveLeaveAsync(int leaveId, int approverId, string comments);
        Task<Leave> RejectLeaveAsync(int leaveId, int approverId, string reason);
        Task<double> CalculateLeaveDaysAsync(DateTime startDate, DateTime endDate);
        Task<bool> HasEnoughBalanceAsync(int employeeId, string leaveType, double days);
        Task<string> GetApplicantWorkflowRoleAsync(Domain.Entities.Core.Employee applicant);
    }
}
