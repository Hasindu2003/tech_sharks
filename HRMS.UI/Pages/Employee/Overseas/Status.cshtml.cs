using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HRMS.Domain.Entities.Leave;
using HRMS.Infrastructure.Identity;
using HRMS.Infrastructure.Persistence;
using HRMS.UI.Services;
using HRMS.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HRMS.UI.Pages.Employee.Overseas
{
    [Authorize]
    public class StatusModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly IOverseasLeaveService _overseasService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly INotificationService _notificationService;

        public StatusModel(ApplicationDbContext context, IOverseasLeaveService overseasService, UserManager<ApplicationUser> userManager, INotificationService notificationService)
        {
            _context = context;
            _overseasService = overseasService;
            _userManager = userManager;
            _notificationService = notificationService;
        }

        public List<Domain.Entities.Leave.Leave> MyOverseasLeaves { get; set; } = new();
        public int EmployeeId { get; set; }
        public string EmployeeGender { get; set; } = "";

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Forbid();
            }

            Domain.Entities.Core.Employee? employee = null;
            if (user.EmployeeId.HasValue)
            {
                employee = await _context.Employees.FindAsync(user.EmployeeId.Value);
            }
            else
            {
                employee = await _context.Employees.FirstOrDefaultAsync(e => e.Email == user.Email);
            }

            if (employee == null || employee.NIC == "DUTY-ACC")
            {
                return Forbid();
            }

            EmployeeId = employee.Id;
            EmployeeGender = employee.Sex ?? "";
            MyOverseasLeaves = await _overseasService.GetEmployeeOverseasLeavesAsync(EmployeeId);
            return Page();
        }

        public async Task<IActionResult> OnPostCancelAsync(int leaveId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Forbid();

            Domain.Entities.Core.Employee? employee = null;
            if (user.EmployeeId.HasValue)
            {
                employee = await _context.Employees.FindAsync(user.EmployeeId.Value);
            }
            else
            {
                employee = await _context.Employees.FirstOrDefaultAsync(e => e.Email == user.Email);
            }

            if (employee == null) return Forbid();

            var leave = await _context.Leaves.Include(l => l.OverseasLeave).FirstOrDefaultAsync(l => l.Id == leaveId && l.EmployeeId == employee.Id);
            if (leave != null && leave.Status == "Pending")
            {
                leave.Status = "Cancelled";
                if (leave.OverseasLeave != null)
                {
                    leave.OverseasLeave.VerificationStatus = "Cancelled";
                    leave.OverseasLeave.BoardApprovalStatus = "Cancelled";
                }
                await _context.SaveChangesAsync();

                // Notify managers/reporting officers of cancellation
                try
                {
                    var roleNames = new[] { "Department Head", "Branch Manager", "Area Manager", "HR Manager", "Admin" };
                    var managerEmails = await (from ur in _context.UserRoles
                                               join r in _context.Roles on ur.RoleId equals r.Id
                                               join u in _context.Users on ur.UserId equals u.Id
                                               select u.Email)
                                              .Distinct()
                                              .ToListAsync();

                    if (employee.ReportingOfficerId.HasValue)
                    {
                        var officer = await _context.Employees.FindAsync(employee.ReportingOfficerId.Value);
                        if (officer != null && !string.IsNullOrEmpty(officer.Email) && !managerEmails.Contains(officer.Email))
                        {
                            managerEmails.Add(officer.Email);
                        }
                    }

                    foreach (var email in managerEmails)
                    {
                        if (string.IsNullOrEmpty(email)) continue;
                        await _notificationService.CreateNotificationAsync(
                            email,
                            "Overseas Leave Request Cancelled",
                            $"{employee.FullName} has cancelled their overseas leave request from {leave.StartDate:MMM dd, yyyy} to {leave.EndDate:MMM dd, yyyy}.",
                            HRMS.Domain.Entities.Core.CoreNotificationType.Info,
                            "/HR/Overseas/Verification"
                        );
                    }
                }
                catch (Exception) { /* Fail silently to not block redirect */ }
            }

            return RedirectToPage();
        }
    }
}
