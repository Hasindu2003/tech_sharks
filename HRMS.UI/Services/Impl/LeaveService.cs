using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HRMS.Domain.Entities.Leave;
using HRMS.Infrastructure.Persistence;
using HRMS.Application.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HRMS.UI.Services.Impl
{
    public class LeaveService : ILeaveService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<LeaveService> _logger;
        private readonly INotificationService _notificationService;

        public LeaveService(ApplicationDbContext context, ILogger<LeaveService> logger, INotificationService notificationService)
        {
            _context = context;
            _logger = logger;
            _notificationService = notificationService;
        }

        public async Task<LeaveEntitlement> GetLeaveBalanceAsync(int employeeId, string leaveType, int year)
        {
            var entitlement = await _context.LeaveEntitlements
                .FirstOrDefaultAsync(e => e.EmployeeId == employeeId && e.LeaveType == leaveType && e.Year == year);

            if (entitlement == null)
            {
                int defaultDays = await GetDefaultLeaveDaysAsync(leaveType);
                entitlement = new LeaveEntitlement
                {
                    EmployeeId = employeeId,
                    LeaveType = leaveType,
                    Year = year,
                    TotalDays = defaultDays,
                    UsedDays = 0,
                    RemainingDays = defaultDays
                };
                _context.LeaveEntitlements.Add(entitlement);
                await _context.SaveChangesAsync();
            }

            return entitlement;
        }

        public async Task<List<LeaveEntitlement>> GetAllLeaveBalancesAsync(int employeeId, int year)
        {
            var leaveTypes = new[] { "Annual", "Casual", "Medical", "Maternity", "Overseas", "Exam", "Bereavement", "Other" };
            var balances = new List<LeaveEntitlement>();

            foreach (var type in leaveTypes)
            {
                var balance = await GetLeaveBalanceAsync(employeeId, type, year);
                balances.Add(balance);
            }

            return balances;
        }

        public async Task<Leave> ApplyLeaveAsync(Leave leave)
        {
            var employee = await _context.Employees
                .Include(e => e.Branch)
                .Include(e => e.Department)
                .FirstOrDefaultAsync(e => e.Id == leave.EmployeeId);

            if (employee == null)
                throw new Exception("Employee not found");

            leave.AppliedDate = DateTime.Now;
            leave.TotalDays = await CalculateLeaveDaysAsync(leave.StartDate, leave.EndDate);
            
            // Set starting status of workflow based on whether department is assigned
            if (employee.DepartmentId == null)
            {
                leave.Status = "PendingBM";
            }
            else
            {
                leave.Status = "Pending"; // Representing stage 1: DH pending
            }

            if (!await HasEnoughBalanceAsync(leave.EmployeeId, leave.LeaveType, leave.TotalDays))
                throw new Exception("Insufficient leave balance");

            _context.Leaves.Add(leave);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Leave applied - EmployeeId: {EmployeeId}, Type: {Type}, Days: {Days}, Status: {Status}", 
                leave.EmployeeId, leave.LeaveType, leave.TotalDays, leave.Status);

            // Send notification to the next required approver(s) in the branch
            try
            {
                if (leave.Status == "PendingBM")
                {
                    await NotifyManagersInBranchAsync(employee.BranchId, "Branch Manager", "New Leave Request (Branch Manager Approval)",
                        $"{employee.FullName} has requested {leave.LeaveType} Leave from {leave.StartDate:MMM dd, yyyy} to {leave.EndDate:MMM dd, yyyy} ({leave.TotalDays} days).", leave.Id);
                }
                else
                {
                    // Notify Department Heads in employee's branch and department
                    var role = await _context.Roles.FirstOrDefaultAsync(r => r.Name == "Department Head");
                    if (role != null)
                    {
                        var dhEmails = await (from ur in _context.UserRoles
                                               join u in _context.Users on ur.UserId equals u.Id
                                               join e in _context.Employees on u.EmployeeId equals e.Id into empGroup
                                               from emp in empGroup.DefaultIfEmpty()
                                               where ur.RoleId == role.Id &&
                                                     (
                                                        (emp != null && emp.BranchId == employee.BranchId && emp.DepartmentId == employee.DepartmentId)
                                                        ||
                                                        (emp == null && u.Branch == (employee.Branch != null ? employee.Branch.Name : null) && u.Department == (employee.Department != null ? employee.Department.Name : null))
                                                     )
                                               select u.Email)
                                              .Distinct()
                                              .ToListAsync();

                        foreach (var email in dhEmails)
                        {
                            if (string.IsNullOrEmpty(email)) continue;
                            await _notificationService.CreateNotificationAsync(
                                email,
                                "New Leave Request",
                                $"{employee.FullName} has requested {leave.LeaveType} Leave from {leave.StartDate:MMM dd, yyyy} to {leave.EndDate:MMM dd, yyyy} ({leave.TotalDays} days).",
                                HRMS.Domain.Entities.Core.CoreNotificationType.Info,
                                $"/Manager/Leave/Review?id={leave.Id}"
                            );
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send notifications for leave request");
            }

            return leave;
        }

        public async Task<List<Leave>> GetEmployeeLeavesAsync(int employeeId)
        {
            return await _context.Leaves
                .Include(l => l.Employee)
                .Where(l => l.EmployeeId == employeeId)
                .OrderByDescending(l => l.AppliedDate)
                .ToListAsync();
        }

        public async Task<List<Leave>> GetPendingApprovalsAsync(int approverId)
        {
            var approverEmp = await _context.Employees.FindAsync(approverId);
            if (approverEmp == null) return new List<Leave>();

            var user = await _context.Users.FirstOrDefaultAsync(u => u.EmployeeId == approverId || u.Email == approverEmp.Email);
            if (user == null) return new List<Leave>();

            var userRoles = await (from ur in _context.UserRoles
                                   join r in _context.Roles on ur.RoleId equals r.Id
                                   where ur.UserId == user.Id
                                   select r.Name)
                                  .ToListAsync();

            return await _context.Leaves
                .Include(l => l.Employee)
                .ThenInclude(e => e.Branch)
                .Include(l => l.Employee)
                .ThenInclude(e => e.Department)
                .Where(l => 
                    (userRoles.Contains("Department Head") && (l.Status == "Pending" || l.Status == "PendingDH") && l.Employee.BranchId == approverEmp.BranchId && l.Employee.DepartmentId == approverEmp.DepartmentId) ||
                    (userRoles.Contains("Branch Manager") && l.Status == "PendingBM" && l.Employee.BranchId == approverEmp.BranchId) ||
                    (userRoles.Contains("HR Manager") && l.Status == "PendingHR" && l.Employee.BranchId == approverEmp.BranchId)
                )
                .OrderBy(l => l.AppliedDate)
                .ToListAsync();
        }

        public async Task<Leave> ApproveLeaveAsync(int leaveId, int approverId, string comments)
        {
            var leave = await _context.Leaves
                .Include(l => l.Employee)
                .FirstOrDefaultAsync(l => l.Id == leaveId);

            if (leave == null)
                throw new Exception("Leave not found");

            var approverEmp = await _context.Employees.FindAsync(approverId);
            if (approverEmp == null)
                throw new Exception("Approver employee profile not found");

            var user = await _context.Users.FirstOrDefaultAsync(u => u.EmployeeId == approverId || u.Email == approverEmp.Email);
            if (user == null)
                throw new Exception("Approver user account not found");

            var userRoles = await (from ur in _context.UserRoles
                                   join r in _context.Roles on ur.RoleId equals r.Id
                                   where ur.UserId == user.Id
                                   select r.Name)
                                  .ToListAsync();

            if (leave.Status == "Pending" || leave.Status == "PendingDH")
            {
                if (!userRoles.Contains("Department Head") || leave.Employee.BranchId != approverEmp.BranchId || leave.Employee.DepartmentId != approverEmp.DepartmentId)
                    throw new Exception("You are not authorized to perform Department Head approval for this leave request.");

                leave.Status = "PendingBM";
                var approvalLog = new LeaveApproval
                {
                    EmployeeId = leave.EmployeeId,
                    LeaveId = leave.Id,
                    ApproverId = approverId,
                    Status = "Approved",
                    Comments = comments,
                    ApprovalDate = DateTime.Now
                };
                _context.LeaveApprovals.Add(approvalLog);
                await _context.SaveChangesAsync();

                // Notify Branch Managers in same branch
                await NotifyManagersInBranchAsync(leave.Employee.BranchId, "Branch Manager", "Leave Request Pending Branch Manager Approval",
                    $"Leave request from {leave.Employee.FullName} ({leave.LeaveType}) has been approved by their Department Head and is pending your approval.", leave.Id);
            }
            else if (leave.Status == "PendingBM")
            {
                if (!userRoles.Contains("Branch Manager") || leave.Employee.BranchId != approverEmp.BranchId)
                    throw new Exception("You are not authorized to perform Branch Manager approval for this leave request.");

                leave.Status = "PendingHR";
                var approvalLog = new LeaveApproval
                {
                    EmployeeId = leave.EmployeeId,
                    LeaveId = leave.Id,
                    ApproverId = approverId,
                    Status = "Approved",
                    Comments = comments,
                    ApprovalDate = DateTime.Now
                };
                _context.LeaveApprovals.Add(approvalLog);
                await _context.SaveChangesAsync();

                // Notify HR Managers in same branch
                await NotifyManagersInBranchAsync(leave.Employee.BranchId, "HR Manager", "Leave Request Pending HR Manager Approval",
                    $"Leave request from {leave.Employee.FullName} ({leave.LeaveType}) has been approved by the Branch Manager and is pending your HR verification.", leave.Id);
            }
            else if (leave.Status == "PendingHR")
            {
                if (!userRoles.Contains("HR Manager") || leave.Employee.BranchId != approverEmp.BranchId)
                    throw new Exception("You are not authorized to perform HR Manager approval for this leave request.");

                leave.Status = "Approved";
                leave.ApprovedById = approverId;
                leave.ApprovedDate = DateTime.Now;

                var entitlement = await GetLeaveBalanceAsync(leave.EmployeeId, leave.LeaveType, leave.StartDate.Year);
                entitlement.UsedDays += leave.TotalDays;
                entitlement.RemainingDays = entitlement.TotalDays - entitlement.UsedDays;

                var approvalLog = new LeaveApproval
                {
                    EmployeeId = leave.EmployeeId,
                    LeaveId = leave.Id,
                    ApproverId = approverId,
                    Status = "Approved",
                    Comments = comments,
                    ApprovalDate = DateTime.Now
                };
                _context.LeaveApprovals.Add(approvalLog);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Leave fully approved - LeaveId: {LeaveId}, ApproverId: {ApproverId}", leaveId, approverId);

                // Notify Employee
                try
                {
                    if (leave.Employee != null && !string.IsNullOrEmpty(leave.Employee.Email))
                    {
                        await _notificationService.CreateNotificationAsync(
                            leave.Employee.Email,
                            "Leave Approved",
                            $"Your leave request from {leave.StartDate:MMM dd, yyyy} to {leave.EndDate:MMM dd, yyyy} has been fully approved.",
                            HRMS.Domain.Entities.Core.CoreNotificationType.Approved,
                            "/Employee/Leave/Status"
                        );
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send leave approval notification");
                }
            }
            else
            {
                throw new Exception("Leave request is not in a status that can be approved.");
            }

            return leave;
        }

        public async Task<Leave> RejectLeaveAsync(int leaveId, int approverId, string reason)
        {
            var leave = await _context.Leaves
                .Include(l => l.Employee)
                .FirstOrDefaultAsync(l => l.Id == leaveId);

            if (leave == null)
                throw new Exception("Leave not found");

            var approverEmp = await _context.Employees.FindAsync(approverId);
            if (approverEmp == null)
                throw new Exception("Approver employee profile not found");

            var user = await _context.Users.FirstOrDefaultAsync(u => u.EmployeeId == approverId || u.Email == approverEmp.Email);
            if (user == null)
                throw new Exception("Approver user account not found");

            var userRoles = await (from ur in _context.UserRoles
                                   join r in _context.Roles on ur.RoleId equals r.Id
                                   where ur.UserId == user.Id
                                   select r.Name)
                                  .ToListAsync();

            // Validate that they are authorized for the current stage
            bool isAuthorized = false;
            if (leave.Status == "Pending" || leave.Status == "PendingDH")
            {
                isAuthorized = userRoles.Contains("Department Head") && 
                               leave.Employee.BranchId == approverEmp.BranchId && 
                               leave.Employee.DepartmentId == approverEmp.DepartmentId;
            }
            else if (leave.Status == "PendingBM")
            {
                isAuthorized = userRoles.Contains("Branch Manager") && 
                               leave.Employee.BranchId == approverEmp.BranchId;
            }
            else if (leave.Status == "PendingHR")
            {
                isAuthorized = userRoles.Contains("HR Manager") && 
                               leave.Employee.BranchId == approverEmp.BranchId;
            }

            if (!isAuthorized)
                throw new Exception("You are not authorized to reject this leave request at this stage of approval.");

            leave.Status = "Rejected";
            leave.ApprovedById = approverId;
            leave.ApprovedDate = DateTime.Now;
            leave.RejectionReason = reason;

            var approvalLog = new LeaveApproval
            {
                EmployeeId = leave.EmployeeId,
                LeaveId = leave.Id,
                ApproverId = approverId,
                Status = "Rejected",
                Comments = reason,
                ApprovalDate = DateTime.Now
            };
            _context.LeaveApprovals.Add(approvalLog);

            await _context.SaveChangesAsync();

            _logger.LogInformation("Leave rejected - LeaveId: {LeaveId}, Reason: {Reason}", leaveId, reason);

            // Send notification to employee
            try
            {
                if (leave.Employee != null && !string.IsNullOrEmpty(leave.Employee.Email))
                {
                    string actorTitle = userRoles.Contains("Department Head") ? "Department Head" :
                                        userRoles.Contains("Branch Manager") ? "Branch Manager" : "HR Manager";

                    await _notificationService.CreateNotificationAsync(
                        leave.Employee.Email,
                        "Leave Rejected",
                        $"Your leave request from {leave.StartDate:MMM dd, yyyy} to {leave.EndDate:MMM dd, yyyy} was rejected by the {actorTitle}. Reason: {reason}",
                        HRMS.Domain.Entities.Core.CoreNotificationType.Rejected,
                        "/Employee/Leave/Status"
                    );
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send leave rejection notification");
            }

            return leave;
        }

        private async Task NotifyManagersInBranchAsync(int branchId, string roleName, string title, string message, int leaveId)
        {
            try
            {
                var role = await _context.Roles.FirstOrDefaultAsync(r => r.Name == roleName);
                if (role == null) return;

                var branch = await _context.Branches.FindAsync(branchId);
                if (branch == null) return;

                var managerEmails = await (from ur in _context.UserRoles
                                           join u in _context.Users on ur.UserId equals u.Id
                                           join e in _context.Employees on u.EmployeeId equals e.Id into empGroup
                                           from emp in empGroup.DefaultIfEmpty()
                                           where ur.RoleId == role.Id &&
                                                 (
                                                    (emp != null && emp.BranchId == branchId)
                                                    ||
                                                    (emp == null && u.Branch == branch.Name)
                                                 )
                                           select u.Email)
                                          .Distinct()
                                          .ToListAsync();

                foreach (var email in managerEmails)
                {
                    if (string.IsNullOrEmpty(email)) continue;
                    await _notificationService.CreateNotificationAsync(
                        email,
                        title,
                        message,
                        HRMS.Domain.Entities.Core.CoreNotificationType.Info,
                        $"/Manager/Leave/Review?id={leaveId}"
                    );
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send notifications to {Role} in branch {BranchId}", roleName, branchId);
            }
        }

        public async Task<int> CalculateLeaveDaysAsync(DateTime startDate, DateTime endDate)
        {
            int days = 0;
            for (var date = startDate.Date; date <= endDate.Date; date = date.AddDays(1))
            {
                if (date.DayOfWeek != DayOfWeek.Saturday && date.DayOfWeek != DayOfWeek.Sunday)
                    days++;
            }
            return days;
        }

        public async Task<bool> HasEnoughBalanceAsync(int employeeId, string leaveType, int days)
        {
            if (leaveType == "Annual" || leaveType == "Casual" || leaveType == "Medical")
            {
                var entitlement = await GetLeaveBalanceAsync(employeeId, leaveType, DateTime.Now.Year);
                return entitlement.RemainingDays >= days;
            }
            return true;
        }

        private async Task<int> GetDefaultLeaveDaysAsync(string leaveType)
        {
            var connection = _context.Database.GetDbConnection();
            bool openedLocally = false;
            if (connection.State != System.Data.ConnectionState.Open)
            {
                await connection.OpenAsync();
                openedLocally = true;
            }

            try
            {
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = "SELECT DefaultDays FROM LeaveAllocationSettings WHERE LeaveType = @leaveType";
                    var param = cmd.CreateParameter();
                    param.ParameterName = "@leaveType";
                    param.Value = leaveType;
                    cmd.Parameters.Add(param);

                    var result = await cmd.ExecuteScalarAsync();
                    if (result != null && result != DBNull.Value)
                    {
                        return Convert.ToInt32(result);
                    }
                }

                // Fallback to defaults
                int defaultDays = leaveType switch
                {
                    "Annual" => 14,
                    "Casual" => 7,
                    "Medical" => 14,
                    "Maternity" => 84,
                    "Overseas" => 30,
                    "Exam" => 7,
                    "Bereavement" => 5,
                    "Other" => 0,
                    _ => 0
                };

                // Seed it into the table so it's visible in settings
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = "INSERT IGNORE INTO LeaveAllocationSettings (LeaveType, DefaultDays) VALUES (@leaveType, @defaultDays)";
                    
                    var param1 = cmd.CreateParameter();
                    param1.ParameterName = "@leaveType";
                    param1.Value = leaveType;
                    cmd.Parameters.Add(param1);

                    var param2 = cmd.CreateParameter();
                    param2.ParameterName = "@defaultDays";
                    param2.Value = defaultDays;
                    cmd.Parameters.Add(param2);

                    await cmd.ExecuteNonQueryAsync();
                }

                return defaultDays;
            }
            finally
            {
                if (openedLocally)
                {
                    await connection.CloseAsync();
                }
            }
        }
    }
}
