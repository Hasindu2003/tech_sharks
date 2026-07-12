using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HRMS.Domain.Entities.Leave;

namespace HRMS.UI.Services
{
    public interface IOverseasLeaveService
    {
        Task<Leave> SubmitOverseasLeaveAsync(Leave leave, OverseasLeave overseasDetails);
        Task<List<Leave>> GetEmployeeOverseasLeavesAsync(int employeeId);
        Task<List<Leave>> GetPendingVerificationsAsync();
        Task<Leave> VerifyOverseasLeaveAsync(int leaveId, string comments, bool approved);
        Task<List<Leave>> GetPendingBoardApprovalsAsync();
        Task<Leave> BoardApproveOverseasLeaveAsync(int leaveId, string comments, bool approved);
    }
}
