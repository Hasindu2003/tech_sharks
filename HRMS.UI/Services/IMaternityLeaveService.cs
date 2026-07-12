using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HRMS.Domain.Entities.Leave;

namespace HRMS.UI.Services
{
    public interface IMaternityLeaveService
    {
        Task<Leave> SubmitMaternityLeaveAsync(Leave leave, MaternityLeave maternityDetails);
        Task<List<Leave>> GetEmployeeMaternityLeavesAsync(int employeeId);
        Task<List<Leave>> GetPendingHrVerificationsAsync();
        Task<Leave> HrVerifyMaternityLeaveAsync(int leaveId, string comments, bool approved);
        Task<List<Leave>> GetPendingAdminApprovalsAsync();
        Task<Leave> AdminApproveMaternityLeaveAsync(int leaveId, string comments, bool approved);
        Task<Leave> ProcessMaternityPayrollAsync(int leaveId, string salaryType, decimal percentage, string nursingConfig);
    }
}
