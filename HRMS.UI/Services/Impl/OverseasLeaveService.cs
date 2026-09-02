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
    public class OverseasLeaveService : IOverseasLeaveService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<OverseasLeaveService> _logger;
        private readonly INotificationService _notificationService;

        public OverseasLeaveService(ApplicationDbContext context, ILogger<OverseasLeaveService> logger, INotificationService notificationService)
        {
            _context = context;
            _logger = logger;
            _notificationService = notificationService;
        }

        public async Task<Leave> SubmitOverseasLeaveAsync(Leave leave, OverseasLeave overseasDetails)
        {
            var employee = await _context.Employees.FindAsync(leave.EmployeeId);
            if (employee == null)
                throw new Exception("Employee not found");

            if (leave.StartDate.Date < DateTime.Today.AddDays(-2))
                throw new Exception("Leave start date cannot be more than 2 days in the past.");

            if (leave.EndDate.Date < leave.StartDate.Date)
                throw new Exception("End date cannot be earlier than start date.");

            if (overseasDetails.PassportExpiry.Date <= leave.EndDate.Date)
                throw new Exception("Passport must be valid until at least after the overseas leave end date.");

            if (string.IsNullOrWhiteSpace(overseasDetails.PassportNumber))
                throw new Exception("Passport number is required.");

            if (string.IsNullOrWhiteSpace(overseasDetails.Country))
                throw new Exception("Destination country is required.");

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

            leave.LeaveType = "Overseas";
            leave.Status = "PendingBM";
            leave.AppliedDate = DateTime.Now;
            leave.TotalDays = (leave.EndDate.Date - leave.StartDate.Date).Days + 1;

            _context.Leaves.Add(leave);
            await _context.SaveChangesAsync();

            overseasDetails.LeaveId = leave.Id;
            overseasDetails.VerificationStatus = "Pending BM";
            overseasDetails.BoardApprovalStatus = "Pending";
            _context.OverseasLeaves.Add(overseasDetails);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Overseas leave submitted - LeaveId: {LeaveId}", leave.Id);

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
                            "New Overseas Leave Request (Branch Manager Approval)",
                            $"{employee.FullName} has requested Overseas Leave to {overseasDetails.Country} ({leave.TotalDays} days) from {leave.StartDate:MMM dd, yyyy} to {leave.EndDate:MMM dd, yyyy}.",
                            HRMS.Domain.Entities.Core.CoreNotificationType.Info,
                            $"/Manager/Leave/Review?id={leave.Id}"
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send notifications for overseas leave request");
            }

            return leave;
        }

        public async Task<List<Leave>> GetEmployeeOverseasLeavesAsync(int employeeId)
        {
            return await _context.Leaves
                .Include(l => l.OverseasLeave)
                .Where(l => l.EmployeeId == employeeId && l.LeaveType == "Overseas")
                .OrderByDescending(l => l.AppliedDate)
                .ToListAsync();
        }

        public async Task<List<Leave>> GetPendingVerificationsAsync()
        {
            return await _context.Leaves
                .Include(l => l.Employee)
                .Include(l => l.OverseasLeave)
                .Where(l => l.LeaveType == "Overseas" && l.Status == "Pending")
                .OrderBy(l => l.AppliedDate)
                .ToListAsync();
        }

        public async Task<Leave> VerifyOverseasLeaveAsync(int leaveId, string comments, bool approved)
        {
            var leave = await _context.Leaves.Include(l => l.OverseasLeave).FirstOrDefaultAsync(l => l.Id == leaveId);
            if (leave == null)
                throw new Exception("Leave not found");

            if (leave.OverseasLeave == null)
                throw new Exception("Overseas details not found");

            leave.OverseasLeave.VerificationComments = comments;

            var leaveWithEmp = await _context.Leaves.Include(l => l.Employee).FirstOrDefaultAsync(l => l.Id == leaveId);
            var empEmail = leaveWithEmp?.Employee?.Email;

            if (approved)
            {
                leave.OverseasLeave.VerificationStatus = "Verified";
                leave.OverseasLeave.BoardApprovalStatus = "Pending Board";

                // Notify Admin/Board members
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
                            "Overseas Leave Verified",
                            $"Overseas leave request for {leaveWithEmp?.Employee?.FullName} has been verified by HR and is pending Board Approval.",
                            HRMS.Domain.Entities.Core.CoreNotificationType.Info,
                            "/Admin/Overseas/BoardApproval"
                        );
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to notify board/admin of overseas verification");
                }

                // Notify Employee
                try
                {
                    if (!string.IsNullOrEmpty(empEmail))
                    {
                        await _notificationService.CreateNotificationAsync(
                            empEmail,
                            "Overseas Request Verified",
                            $"Your overseas travel details have been verified by HR. Pending final board approval.",
                            HRMS.Domain.Entities.Core.CoreNotificationType.Info,
                            "/Employee/Overseas/Status"
                        );
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to notify employee of overseas verification");
                }
            }
            else
            {
                leave.OverseasLeave.VerificationStatus = "Rejected";
                leave.Status = "Rejected";

                // Notify Employee
                try
                {
                    if (!string.IsNullOrEmpty(empEmail))
                    {
                        await _notificationService.CreateNotificationAsync(
                            empEmail,
                            "Overseas Request Rejected",
                            $"Your overseas leave request was rejected by HR. Reason: {comments}",
                            HRMS.Domain.Entities.Core.CoreNotificationType.Rejected,
                            "/Employee/Overseas/Status"
                        );
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to notify employee of overseas rejection");
                }
            }

            await _context.SaveChangesAsync();
            return leave;
        }

        public async Task<List<Leave>> GetPendingBoardApprovalsAsync()
        {
            return await _context.Leaves
                .Include(l => l.Employee)
                .Include(l => l.OverseasLeave)
                .Where(l => l.LeaveType == "Overseas" && 
                           l.OverseasLeave != null && 
                           l.OverseasLeave.VerificationStatus == "Verified" &&
                           l.OverseasLeave.BoardApprovalStatus == "Pending Board")
                .OrderBy(l => l.AppliedDate)
                .ToListAsync();
        }

        public async Task<Leave> BoardApproveOverseasLeaveAsync(int leaveId, string comments, bool approved)
        {
            var leave = await _context.Leaves.Include(l => l.OverseasLeave).FirstOrDefaultAsync(l => l.Id == leaveId);
            if (leave == null)
                throw new Exception("Leave not found");

            if (leave.OverseasLeave == null)
                throw new Exception("Overseas details not found");

            var leaveWithEmp = await _context.Leaves.Include(l => l.Employee).FirstOrDefaultAsync(l => l.Id == leaveId);
            var empEmail = leaveWithEmp?.Employee?.Email;

            if (approved)
            {
                leave.OverseasLeave.BoardApprovalStatus = "Approved";
                leave.OverseasLeave.BoardRejectionReason = comments;
                leave.Status = "Approved";

                // Notify Employee
                try
                {
                    if (!string.IsNullOrEmpty(empEmail))
                    {
                        await _notificationService.CreateNotificationAsync(
                            empEmail,
                            "Overseas Request Approved",
                            $"Your overseas leave request has been approved by the Board.",
                            HRMS.Domain.Entities.Core.CoreNotificationType.Approved,
                            "/Employee/Overseas/Status"
                        );
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to notify employee of overseas approval");
                }
            }
            else
            {
                leave.OverseasLeave.BoardApprovalStatus = "Rejected";
                leave.OverseasLeave.BoardRejectionReason = comments;
                leave.Status = "Rejected";

                // Notify Employee
                try
                {
                    if (!string.IsNullOrEmpty(empEmail))
                    {
                        await _notificationService.CreateNotificationAsync(
                            empEmail,
                            "Overseas Request Rejected",
                            $"Your overseas leave request was rejected by the Board. Reason: {comments}",
                            HRMS.Domain.Entities.Core.CoreNotificationType.Rejected,
                            "/Employee/Overseas/Status"
                        );
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to notify employee of overseas rejection");
                }
            }

            await _context.SaveChangesAsync();
            return leave;
        }
    }
}
