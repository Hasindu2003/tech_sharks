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

namespace HRMS.UI.Pages.Employee.Leave
{
    [Authorize]
    public class StatusModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly ILeaveService _leaveService;
        private readonly IOverseasLeaveService _overseasService;
        private readonly IMaternityLeaveService _maternityService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly INotificationService _notificationService;

        public StatusModel(
            ApplicationDbContext context, 
            ILeaveService leaveService, 
            IOverseasLeaveService overseasService,
            IMaternityLeaveService maternityService,
            UserManager<ApplicationUser> userManager, 
            INotificationService notificationService)
        {
            _context = context;
            _leaveService = leaveService;
            _overseasService = overseasService;
            _maternityService = maternityService;
            _userManager = userManager;
            _notificationService = notificationService;
        }

        public List<Domain.Entities.Leave.Leave> MyLeaves { get; set; } = new();
        public List<Domain.Entities.Leave.Leave> MyOverseasLeaves { get; set; } = new();
        public List<Domain.Entities.Leave.Leave> MyMaternityLeaves { get; set; } = new();
        public int EmployeeId { get; set; }
        public string EmployeeGender { get; set; } = "";

        [BindProperty]
        public string ActiveTab { get; set; } = "standard";

        public async Task<IActionResult> OnGetAsync(string tab = "standard")
        {
            ActiveTab = tab;
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

            MyLeaves = await _leaveService.GetEmployeeLeavesAsync(EmployeeId);
            MyLeaves = MyLeaves.Where(l => l.LeaveType != "Maternity" && l.LeaveType != "Overseas").ToList();

            MyOverseasLeaves = await _overseasService.GetEmployeeOverseasLeavesAsync(EmployeeId);
            MyMaternityLeaves = await _maternityService.GetEmployeeMaternityLeavesAsync(EmployeeId);

            return Page();
        }

        public async Task<IActionResult> OnPostCancelAsync(int leaveId, string activeTab = "standard")
        {
            ActiveTab = activeTab;
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Forbid();

            Domain.Entities.Core.Employee? employee = null;
            if (user.EmployeeId.HasValue)
            {
                employee = await _context.Employees
                    .Include(e => e.Branch)
                    .Include(e => e.Department)
                    .FirstOrDefaultAsync(e => e.Id == user.EmployeeId.Value);
            }
            else
            {
                employee = await _context.Employees
                    .Include(e => e.Branch)
                    .Include(e => e.Department)
                    .FirstOrDefaultAsync(e => e.Email == user.Email);
            }

            if (employee == null) return Forbid();

            var leave = await _context.Leaves
                .Include(l => l.MaternityLeave)
                .Include(l => l.OverseasLeave)
                .FirstOrDefaultAsync(l => l.Id == leaveId && l.EmployeeId == employee.Id);

            if (leave != null && (leave.Status == "Pending" || leave.Status == "PendingDH" || leave.Status == "PendingBM" || leave.Status == "PendingHR"))
            {
                string oldStatus = leave.Status;
                leave.Status = "Cancelled";
                
                string targetUrl = "/Manager/Leave/Approval";
                string titlePrefix = "Leave Request";

                if (leave.LeaveType == "Maternity")
                {
                    titlePrefix = "Maternity Leave Request";
                    targetUrl = "/HR/Maternity/Verification";
                    if (leave.MaternityLeave != null)
                    {
                        leave.MaternityLeave.VerificationStatus = "Cancelled";
                    }
                }
                else if (leave.LeaveType == "Overseas")
                {
                    titlePrefix = "Overseas Leave Request";
                    targetUrl = "/HR/Overseas/Verification";
                    if (leave.OverseasLeave != null)
                    {
                        leave.OverseasLeave.VerificationStatus = "Cancelled";
                        leave.OverseasLeave.BoardApprovalStatus = "Cancelled";
                    }
                }

                await _context.SaveChangesAsync();

                // Notify managers/reporting officers of cancellation based on current stage in same branch
                try
                {
                    var role = "";
                    bool checkDept = false;
                    
                    if (oldStatus == "Pending" || oldStatus == "PendingDH")
                    {
                        role = "Department Head";
                        checkDept = true;
                    }
                    else if (oldStatus == "PendingBM")
                    {
                        role = "Branch Manager";
                    }
                    else if (oldStatus == "PendingHR")
                    {
                        role = "HR Manager";
                    }

                    List<string> managerEmails = new List<string>();

                    if (!string.IsNullOrEmpty(role))
                    {
                        var roleObj = await _context.Roles.FirstOrDefaultAsync(r => r.Name == role);
                        if (roleObj != null)
                        {
                            managerEmails = await (from ur in _context.UserRoles
                                                   join u in _context.Users on ur.UserId equals u.Id
                                                   join e in _context.Employees on u.EmployeeId equals e.Id into empGroup
                                                   from emp in empGroup.DefaultIfEmpty()
                                                   where ur.RoleId == roleObj.Id &&
                                                         (
                                                            (emp != null && emp.BranchId == employee.BranchId && (!checkDept || emp.DepartmentId == employee.DepartmentId))
                                                            ||
                                                            (emp == null && u.Branch == (employee.Branch != null ? employee.Branch.Name : null) && (!checkDept || u.Department == (employee.Department != null ? employee.Department.Name : null)))
                                                         )
                                                   select u.Email)
                                                  .Distinct()
                                                  .ToListAsync();
                        }
                    }
                    else if (leave.LeaveType == "Maternity" || leave.LeaveType == "Overseas")
                    {
                        // Fallback: Notify HR Managers in the same branch
                        var roleObj = await _context.Roles.FirstOrDefaultAsync(r => r.Name == "HR Manager");
                        if (roleObj != null)
                        {
                            managerEmails = await (from ur in _context.UserRoles
                                                   join u in _context.Users on ur.UserId equals u.Id
                                                   join e in _context.Employees on u.EmployeeId equals e.Id into empGroup
                                                   from emp in empGroup.DefaultIfEmpty()
                                                   where ur.RoleId == roleObj.Id &&
                                                         (
                                                            (emp != null && emp.BranchId == employee.BranchId)
                                                            ||
                                                            (emp == null && u.Branch == (employee.Branch != null ? employee.Branch.Name : null))
                                                         )
                                                   select u.Email)
                                                  .Distinct()
                                                  .ToListAsync();
                        }
                    }

                    foreach (var email in managerEmails)
                    {
                        if (string.IsNullOrEmpty(email)) continue;
                        await _notificationService.CreateNotificationAsync(
                            email,
                            $"{titlePrefix} Cancelled",
                            $"{employee.FullName} has cancelled their leave request from {leave.StartDate:MMM dd, yyyy} to {leave.EndDate:MMM dd, yyyy}.",
                            HRMS.Domain.Entities.Core.CoreNotificationType.Info,
                            targetUrl
                        );
                    }
                }
                catch (Exception) { /* Fail silently to not block redirect */ }
            }

            return RedirectToPage(new { tab = ActiveTab });
        }
    }
}
