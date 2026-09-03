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
    public class MaternityLeaveService : IMaternityLeaveService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<MaternityLeaveService> _logger;
        private readonly INotificationService _notificationService;

        public MaternityLeaveService(ApplicationDbContext context, ILogger<MaternityLeaveService> _logger, INotificationService notificationService)
        {
            this._context = context;
            this._logger = _logger;
            this._notificationService = notificationService;
        }

        public async Task<Leave> SubmitMaternityLeaveAsync(Leave leave, MaternityLeave maternityDetails)
        {
            var employee = await _context.Employees.FindAsync(leave.EmployeeId);
            if (employee == null)
                throw new Exception("Employee not found");

            if (employee.Sex != null && employee.Sex.Equals("Male", StringComparison.OrdinalIgnoreCase))
                throw new Exception("Male employees are not eligible for Maternity Leave.");

            if (leave.StartDate.Date < DateTime.Today.AddDays(-2))
                throw new Exception("Leave start date cannot be more than 2 days in the past.");

            if (leave.EndDate.Date < leave.StartDate.Date)
                throw new Exception("End date cannot be earlier than start date.");

            var overlappingLeave = await _context.Leaves
                .FirstOrDefaultAsync(l => l.EmployeeId == leave.EmployeeId &&
                                          l.Id != leave.Id &&
                                          l.Status != "Rejected" &&
                                          l.Status != "Cancelled" &&
                                          l.StartDate.Date <= leave.EndDate.Date &&
                                          l.EndDate.Date >= leave.StartDate.Date);
            if (overlappingLeave != null)
            {
                throw new Exception($"You already have an active leave request ({overlappingLeave.LeaveType}: {overlappingLeave.StartDate:yyyy-MM-dd} to {overlappingLeave.EndDate:yyyy-MM-dd}, Status: {overlappingLeave.Status}) overlapping with the selected dates.");
            }

            leave.LeaveType = "Maternity";
            leave.Status = "PendingBM";
            leave.AppliedDate = DateTime.Now;
            leave.TotalDays = (leave.EndDate.Date - leave.StartDate.Date).Days + 1;

            _context.Leaves.Add(leave);
            await _context.SaveChangesAsync();

            maternityDetails.LeaveId = leave.Id;
            maternityDetails.VerificationStatus = "Pending BM";
            _context.MaternityLeaves.Add(maternityDetails);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Maternity leave submitted - LeaveId: {LeaveId}", leave.Id);

            // Notify Branch Manager duty account in employee's branch
            try
            {
                var bmRole = await _context.Roles.FirstOrDefaultAsync(r => r.Name == "Branch Manager");
                if (bmRole != null)
                {
                    var branch = await _context.Branches.FindAsync(employee.BranchId);
                    var bmUsers = await (from ur in _context.UserRoles
                                         join u in _context.Users on ur.UserId equals u.Id
                                         where ur.RoleId == bmRole.Id
                                         select u).ToListAsync();

                    var targetEmails = new List<string>();
                    foreach (var u in bmUsers)
                    {
                        if (string.IsNullOrEmpty(u.Email)) continue;
                        if (!string.IsNullOrEmpty(employee.Email) && u.Email.Equals(employee.Email, StringComparison.OrdinalIgnoreCase)) continue;

                        bool branchMatches = (!string.IsNullOrEmpty(u.Branch) && (u.Branch.Equals(branch?.Name, StringComparison.OrdinalIgnoreCase) || u.Branch == employee.BranchId.ToString()));
                        if (branchMatches)
                        {
                            targetEmails.Add(u.Email);
                        }
                        else if (u.EmployeeId.HasValue)
                        {
                            var emp = await _context.Employees.FindAsync(u.EmployeeId.Value);
                            if (emp != null && !emp.NIC.StartsWith("DUTY") && emp.NIC != "DUTY-ACC" && emp.BranchId == employee.BranchId)
                            {
                                targetEmails.Add(u.Email);
                            }
                        }
                    }

                    foreach (var email in targetEmails.Distinct())
                    {
                        await _notificationService.CreateNotificationAsync(
                            email,
                            "New Maternity Leave Request (Branch Manager Approval)",
                            $"{employee.FullName} has requested Maternity Leave (Child #{maternityDetails.ChildNumber}, {leave.TotalDays} days) from {leave.StartDate:MMM dd, yyyy} to {leave.EndDate:MMM dd, yyyy}.",
                            HRMS.Domain.Entities.Core.CoreNotificationType.Info,
                            $"/Manager/Leave/Review?id={leave.Id}"
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send notifications for maternity leave request");
            }

            return leave;
        }

        public async Task<List<Leave>> GetEmployeeMaternityLeavesAsync(int employeeId)
        {
            return await _context.Leaves
                .Include(l => l.MaternityLeave)
                .Include(l => l.MaternityPayment)
                .Where(l => l.EmployeeId == employeeId && l.LeaveType == "Maternity")
                .OrderByDescending(l => l.AppliedDate)
                .ToListAsync();
        }

        public async Task<List<Leave>> GetPendingHrVerificationsAsync()
        {
            return await _context.Leaves
                .Include(l => l.Employee)
                .Include(l => l.MaternityLeave)
                .Where(l => l.LeaveType == "Maternity" && l.Status == "Pending")
                .OrderBy(l => l.AppliedDate)
                .ToListAsync();
        }

        public async Task<Leave> HrVerifyMaternityLeaveAsync(int leaveId, string comments, bool approved)
        {
            var leave = await _context.Leaves.Include(l => l.MaternityLeave).FirstOrDefaultAsync(l => l.Id == leaveId);
            if (leave == null)
                throw new Exception("Leave not found");

            if (leave.MaternityLeave == null)
                throw new Exception("Maternity details not found");

            leave.MaternityLeave.VerificationComments = comments;

            var leaveWithEmp = await _context.Leaves.Include(l => l.Employee).FirstOrDefaultAsync(l => l.Id == leaveId);
            var empEmail = leaveWithEmp?.Employee?.Email;

            if (approved)
            {
                leave.MaternityLeave.VerificationStatus = "HR Verified";

                // Notify Admin users
                try
                {
                    var adminEmails = await (from ur in _context.UserRoles
                                               join r in _context.Roles on ur.RoleId equals r.Id
                                               join u in _context.Users on ur.UserId equals u.Id
                                               where r.Name == "Admin"
                                               select u.Email)
                                              .Distinct()
                                              .ToListAsync();

                    foreach (var email in adminEmails)
                    {
                        if (string.IsNullOrEmpty(email)) continue;
                        await _notificationService.CreateNotificationAsync(
                            email,
                            "Maternity Leave Verified",
                            $"Maternity leave request for {leaveWithEmp?.Employee?.FullName} has been verified by HR and is pending final approval.",
                            HRMS.Domain.Entities.Core.CoreNotificationType.Info,
                            "/Admin/Maternity/AdminApproval"
                        );
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to notify admin of maternity verification");
                }

                // Notify Employee
                try
                {
                    if (!string.IsNullOrEmpty(empEmail))
                    {
                        await _notificationService.CreateNotificationAsync(
                            empEmail,
                            "Maternity Request Verified",
                            $"Your maternity leave details have been verified by HR. Pending final board/admin approval.",
                            HRMS.Domain.Entities.Core.CoreNotificationType.Info,
                            "/Employee/Maternity/Status"
                        );
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to notify employee of maternity verification");
                }
            }
            else
            {
                leave.MaternityLeave.VerificationStatus = "Rejected";
                leave.Status = "Rejected";

                // Notify Employee
                try
                {
                    if (!string.IsNullOrEmpty(empEmail))
                    {
                        await _notificationService.CreateNotificationAsync(
                            empEmail,
                            "Maternity Request Rejected",
                            $"Your maternity leave request was rejected by HR. Reason: {comments}",
                            HRMS.Domain.Entities.Core.CoreNotificationType.Rejected,
                            "/Employee/Maternity/Status"
                        );
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to notify employee of maternity rejection");
                }
            }

            await _context.SaveChangesAsync();
            return leave;
        }

        public async Task<List<Leave>> GetPendingAdminApprovalsAsync()
        {
            return await _context.Leaves
                .Include(l => l.Employee)
                .Include(l => l.MaternityLeave)
                .Where(l => l.LeaveType == "Maternity" && 
                           l.MaternityLeave != null && 
                           l.MaternityLeave.VerificationStatus == "HR Verified")
                .OrderBy(l => l.AppliedDate)
                .ToListAsync();
        }

        public async Task<Leave> AdminApproveMaternityLeaveAsync(int leaveId, string comments, bool approved)
        {
            var leave = await _context.Leaves.Include(l => l.MaternityLeave).FirstOrDefaultAsync(l => l.Id == leaveId);
            if (leave == null)
                throw new Exception("Leave not found");

            var leaveWithEmp = await _context.Leaves.Include(l => l.Employee).FirstOrDefaultAsync(l => l.Id == leaveId);
            var empEmail = leaveWithEmp?.Employee?.Email;

            if (approved)
            {
                leave.Status = "Approved";

                // Notify Employee
                try
                {
                    if (!string.IsNullOrEmpty(empEmail))
                    {
                        await _notificationService.CreateNotificationAsync(
                            empEmail,
                            "Maternity Request Approved",
                            $"Your maternity leave request has been approved.",
                            HRMS.Domain.Entities.Core.CoreNotificationType.Approved,
                            "/Employee/Maternity/Status"
                        );
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to notify employee of maternity approval");
                }
            }
            else
            {
                leave.Status = "Rejected";

                // Notify Employee
                try
                {
                    if (!string.IsNullOrEmpty(empEmail))
                    {
                        await _notificationService.CreateNotificationAsync(
                            empEmail,
                            "Maternity Request Rejected",
                            $"Your maternity leave request was rejected. Reason: {comments}",
                            HRMS.Domain.Entities.Core.CoreNotificationType.Rejected,
                            "/Employee/Maternity/Status"
                        );
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to notify employee of maternity rejection");
                }
            }

            if (leave.MaternityLeave != null)
            {
                leave.MaternityLeave.VerificationComments += $"\nAdmin: {comments}";
            }

            await _context.SaveChangesAsync();
            return leave;
        }

        public async Task<Leave> ProcessMaternityPayrollAsync(int leaveId, string salaryType, decimal percentage, string nursingConfig)
        {
            var leave = await _context.Leaves
                .Include(l => l.MaternityPayment)
                .FirstOrDefaultAsync(l => l.Id == leaveId);
            if (leave == null)
                throw new Exception("Leave not found");

            var payment = leave.MaternityPayment ?? new MaternityPayment { LeaveId = leaveId };
            payment.SalaryAdjustmentType = salaryType;
            if (string.Equals(salaryType, "Full", StringComparison.OrdinalIgnoreCase))
                payment.SalaryPercentage = 100m;
            else if (string.Equals(salaryType, "NoPay", StringComparison.OrdinalIgnoreCase))
                payment.SalaryPercentage = 0m;
            else
                payment.SalaryPercentage = (percentage > 0m && percentage < 100m) ? percentage : 50m;
            payment.NursingBreakConfig = nursingConfig;
            payment.Status = "Processed";
            payment.PaymentDate = DateTime.Now;

            if (leave.MaternityPayment == null)
            {
                _context.MaternityPayments.Add(payment);
            }

            await _context.SaveChangesAsync();
            return leave;
        }
    }
}
