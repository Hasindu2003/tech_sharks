using HRMS.Domain.Entities.Core;
using HRMS.Infrastructure.Identity;
using HRMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HRMS.Application.Services
{
    public interface ITrainingNotificationService
    {
        Task NotifyTrainingRequestSubmittedAsync(int requestId, int employeeId, string programTitle);
        Task NotifyTrainingRequestDecisionAsync(int requestId, string status);
        Task NotifySessionScheduledAsync(int trainingId, List<int> attendeeEmployeeIds, int branchId);
        Task NotifySessionUpdatedAsync(int trainingId, List<int> attendeeEmployeeIds);
        Task NotifySessionStatusChangedAsync(int trainingId, string newStatus);
    }

    public class TrainingNotificationService : ITrainingNotificationService
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly INotificationService _notificationService;

        public TrainingNotificationService(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            INotificationService notificationService)
        {
            _context = context;
            _userManager = userManager;
            _notificationService = notificationService;
        }

        public async Task NotifyTrainingRequestSubmittedAsync(int requestId, int employeeId, string programTitle)
        {
            try
            {
                var emp = await _context.Employees
                    .Include(e => e.Branch)
                    .FirstOrDefaultAsync(e => e.Id == employeeId);

                var empName = emp?.FullName ?? "An employee";
                var epf = emp?.EPFNumber ?? "";
                var branchId = emp?.BranchId ?? 0;
                var branchName = emp?.Branch?.Name ?? "";

                var recipientUserIds = new HashSet<string>();

                // 1. Find Branch Managers of the employee's branch
                var bmRole = await _context.Roles.FirstOrDefaultAsync(r => r.Name == "Branch Manager");
                if (bmRole != null)
                {
                    var bmUsers = await (from ur in _context.UserRoles
                                         join u in _context.Users on ur.UserId equals u.Id
                                         where ur.RoleId == bmRole.Id
                                         select u).ToListAsync();

                    foreach (var bm in bmUsers)
                    {
                        bool matches = false;
                        if (!string.IsNullOrWhiteSpace(bm.Branch) &&
                            (string.Equals(bm.Branch.Trim(), branchName, StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(bm.Branch.Trim(), branchId.ToString(), StringComparison.OrdinalIgnoreCase)))
                        {
                            matches = true;
                        }

                        if (!matches && bm.EmployeeId.HasValue)
                        {
                            var bmEmp = await _context.Employees.FirstOrDefaultAsync(e => e.Id == bm.EmployeeId.Value);
                            if (bmEmp != null && bmEmp.BranchId == branchId)
                            {
                                matches = true;
                            }
                        }

                        if (!matches && !string.IsNullOrWhiteSpace(bm.ManagedBranches))
                        {
                            var tokens = bm.ManagedBranches.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                            matches = tokens.Any(t => t == branchId.ToString() || string.Equals(t, branchName, StringComparison.OrdinalIgnoreCase));
                        }

                        if (matches && !string.IsNullOrWhiteSpace(bm.Id))
                        {
                            recipientUserIds.Add(bm.Id);
                        }
                    }
                }

                // 2. Find Area Managers assigned to the employee's branch
                var amRole = await _context.Roles.FirstOrDefaultAsync(r => r.Name == "Area Manager");
                if (amRole != null)
                {
                    var amUsers = await (from ur in _context.UserRoles
                                         join u in _context.Users on ur.UserId equals u.Id
                                         where ur.RoleId == amRole.Id
                                         select u).ToListAsync();

                    foreach (var am in amUsers)
                    {
                        bool matches = false;
                        if (!string.IsNullOrWhiteSpace(am.ManagedBranches))
                        {
                            var tokens = am.ManagedBranches.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                            matches = tokens.Any(t => t == branchId.ToString() || string.Equals(t, branchName, StringComparison.OrdinalIgnoreCase));
                        }

                        if (!matches && !string.IsNullOrWhiteSpace(am.Branch) &&
                            (string.Equals(am.Branch.Trim(), branchName, StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(am.Branch.Trim(), branchId.ToString(), StringComparison.OrdinalIgnoreCase)))
                        {
                            matches = true;
                        }

                        if (!matches && am.EmployeeId.HasValue)
                        {
                            var amEmp = await _context.Employees.FirstOrDefaultAsync(e => e.Id == am.EmployeeId.Value);
                            if (amEmp != null && amEmp.BranchId == branchId)
                            {
                                matches = true;
                            }
                        }

                        // Fallback: If an Area Manager has no specific branches configured, treat as overseeing all branches
                        if (!matches && string.IsNullOrWhiteSpace(am.ManagedBranches) && string.IsNullOrWhiteSpace(am.Branch) && !am.EmployeeId.HasValue)
                        {
                            matches = true;
                        }

                        if (matches && !string.IsNullOrWhiteSpace(am.Id))
                        {
                            recipientUserIds.Add(am.Id);
                        }
                    }
                }

                var title = "New Training Request";
                var message = $"{empName} (EPF: {epf}) from {branchName} submitted a request to attend '{programTitle}'.";
                var targetUrl = $"/Training/Details?id={requestId}";

                foreach (var userId in recipientUserIds)
                {
                    await _notificationService.CreateNotificationAsync(userId, title, message, CoreNotificationType.Info, targetUrl);
                }
            }
            catch
            {
                // Prevent notification errors from interrupting user transactions
            }
        }

        public async Task NotifyTrainingRequestDecisionAsync(int requestId, string status)
        {
            try
            {
                var req = await _context.TrainingProgramRequests
                    .Include(r => r.Employee)
                        .ThenInclude(e => e.Branch)
                    .FirstOrDefaultAsync(r => r.Id == requestId);

                if (req == null || req.Employee == null) return;

                var emp = req.Employee;
                var programTitle = req.Title ?? "Training Program";

                // 1. Notify the Employee
                var empUser = await _userManager.Users.FirstOrDefaultAsync(u => u.EmployeeId == emp.Id || u.Email == emp.Email || u.UserName == emp.Email);
                var empRecipient = empUser?.Id ?? empUser?.Email ?? emp.Email;

                if (!string.IsNullOrWhiteSpace(empRecipient))
                {
                    if (string.Equals(status, "Approved", StringComparison.OrdinalIgnoreCase))
                    {
                        await _notificationService.CreateNotificationAsync(
                            empRecipient,
                            "Training Request Approved",
                            $"Your training application for '{programTitle}' has been approved.",
                            CoreNotificationType.Approved,
                            "/Training/Sessions");
                    }
                    else
                    {
                        await _notificationService.CreateNotificationAsync(
                            empRecipient,
                            "Training Request Declined",
                            $"Your training application for '{programTitle}' was not approved at this time.",
                            CoreNotificationType.Rejected,
                            "/Training/Sessions");
                    }
                }
            }
            catch
            {
            }
        }

        public async Task NotifySessionScheduledAsync(int trainingId, List<int> attendeeEmployeeIds, int branchId)
        {
            try
            {
                var session = await _context.Trainings.FindAsync(trainingId);
                if (session == null) return;

                var dateStr = session.Date.ToString("dd MMM yyyy");
                var timeStr = DateTime.Today.Add(session.StartTime).ToString("hh:mm tt");
                var venue = session.Location ?? "Scheduled Venue";
                var title = session.Title;

                // 1. Notify Enrolled Employees
                if (attendeeEmployeeIds != null && attendeeEmployeeIds.Any())
                {
                    var employees = await _context.Employees
                        .Where(e => attendeeEmployeeIds.Contains(e.Id))
                        .ToListAsync();

                    var empIds = employees.Select(e => e.Id).ToList();
                    var users = await _userManager.Users.Where(u => u.EmployeeId.HasValue && empIds.Contains(u.EmployeeId.Value)).ToListAsync();

                    foreach (var emp in employees)
                    {
                        var user = users.FirstOrDefault(u => u.EmployeeId == emp.Id);
                        var email = user?.Email ?? emp.Email;
                        if (!string.IsNullOrWhiteSpace(email))
                        {
                            await _notificationService.CreateNotificationAsync(
                                email,
                                $"Training Scheduled: {title}",
                                $"You have been enrolled in '{title}' on {dateStr} at {timeStr} ({venue}).",
                                CoreNotificationType.Info,
                                $"/Training/SessionDetails?id={trainingId}");
                        }
                    }
                }

                // 2. Notify the Branch Manager
                var branch = await _context.Branches.FindAsync(branchId);
                var branchName = branch?.Name ?? "";
                var branchManagers = await _userManager.GetUsersInRoleAsync("Branch Manager");
                var targetBms = branchManagers.Where(bm => string.Equals(bm.Branch, branchName, StringComparison.OrdinalIgnoreCase));

                foreach (var bm in targetBms)
                {
                    if (!string.IsNullOrWhiteSpace(bm.Email))
                    {
                        await _notificationService.CreateNotificationAsync(
                            bm.Email,
                            $"New Training Session: {title}",
                            $"A session of '{title}' has been scheduled for {branchName} on {dateStr} at {timeStr}.",
                            CoreNotificationType.Info,
                            $"/Training/SessionDetails?id={trainingId}");
                    }
                }
            }
            catch
            {
            }
        }

        public async Task NotifySessionUpdatedAsync(int trainingId, List<int> attendeeEmployeeIds)
        {
            try
            {
                var session = await _context.Trainings.FindAsync(trainingId);
                if (session == null) return;

                var dateStr = session.Date.ToString("dd MMM yyyy");
                var timeStr = DateTime.Today.Add(session.StartTime).ToString("hh:mm tt");
                var venue = session.Location ?? "Scheduled Venue";
                var title = session.Title;

                if (attendeeEmployeeIds != null && attendeeEmployeeIds.Any())
                {
                    var employees = await _context.Employees
                        .Where(e => attendeeEmployeeIds.Contains(e.Id))
                        .ToListAsync();

                    var empIds = employees.Select(e => e.Id).ToList();
                    var users = await _userManager.Users.Where(u => u.EmployeeId.HasValue && empIds.Contains(u.EmployeeId.Value)).ToListAsync();

                    foreach (var emp in employees)
                    {
                        var user = users.FirstOrDefault(u => u.EmployeeId == emp.Id);
                        var email = user?.Email ?? emp.Email;
                        if (!string.IsNullOrWhiteSpace(email))
                        {
                            await _notificationService.CreateNotificationAsync(
                                email,
                                $"Training Updated: {title}",
                                $"Session details for '{title}' have been updated: {dateStr} at {timeStr} ({venue}).",
                                CoreNotificationType.Info,
                                $"/Training/SessionDetails?id={trainingId}");
                        }
                    }
                }
            }
            catch
            {
            }
        }

        public async Task NotifySessionStatusChangedAsync(int trainingId, string newStatus)
        {
            try
            {
                var session = await _context.Trainings
                    .Include(t => t.EmployeeTrainings)
                        .ThenInclude(et => et.Employee)
                    .FirstOrDefaultAsync(t => t.Id == trainingId);

                if (session == null) return;

                var title = session.Title;
                var dateStr = session.Date.ToString("dd MMM yyyy");

                var employees = session.EmployeeTrainings.Where(et => et.Employee != null).Select(et => et.Employee).ToList();
                var empIds = employees.Select(e => e.Id).ToList();
                var users = await _userManager.Users.Where(u => u.EmployeeId.HasValue && empIds.Contains(u.EmployeeId.Value)).ToListAsync();

                foreach (var emp in employees)
                {
                    var user = users.FirstOrDefault(u => u.EmployeeId == emp.Id) 
                               ?? await _userManager.FindByEmailAsync(emp.Email ?? "")
                               ?? await _userManager.FindByNameAsync(emp.Email ?? "");
                    var email = user?.Email ?? emp.Email;
                    if (!string.IsNullOrWhiteSpace(email))
                    {
                        if (string.Equals(newStatus, "Completed", StringComparison.OrdinalIgnoreCase))
                        {
                            await _notificationService.CreateNotificationAsync(
                                email,
                                $"Training Completed: Feedback Requested - {title}",
                                $"The training session '{title}' held on {dateStr} has been marked as Completed. Please provide your feedback and rating.",
                                CoreNotificationType.Approved,
                                $"/Training/SessionDetails?id={trainingId}#feedbackSection");
                        }
                        else if (string.Equals(newStatus, "Cancelled", StringComparison.OrdinalIgnoreCase))
                        {
                            await _notificationService.CreateNotificationAsync(
                                email,
                                $"Training Cancelled: {title}",
                                $"The training session '{title}' scheduled for {dateStr} has been cancelled.",
                                CoreNotificationType.Rejected,
                                $"/Training/SessionDetails?id={trainingId}");
                        }
                    }
                }
            }
            catch
            {
            }
        }
    }
}
