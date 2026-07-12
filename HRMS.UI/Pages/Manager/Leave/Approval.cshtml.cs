using System.Collections.Generic;
using System.Threading.Tasks;
using HRMS.Domain.Entities.Leave;
using HRMS.Infrastructure.Identity;
using HRMS.Infrastructure.Persistence;
using HRMS.UI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HRMS.UI.Pages.Manager.Leave
{
    [Authorize(Roles = "Department Head, Branch Manager, Area Manager, HR Manager")]
    public class ApprovalModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly ILeaveService _leaveService;
        private readonly UserManager<ApplicationUser> _userManager;

        public ApprovalModel(ApplicationDbContext context, ILeaveService leaveService, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _leaveService = leaveService;
            _userManager = userManager;
        }

        public class ReviewedLeaveViewModel
        {
            public int LeaveId { get; set; }
            public string EmployeeName { get; set; } = null!;
            public string LeaveType { get; set; } = null!;
            public DateTime StartDate { get; set; }
            public DateTime EndDate { get; set; }
            public int TotalDays { get; set; }
            public string ActionTaken { get; set; } = null!; // Approved / Rejected
            public string? Comments { get; set; }
            public DateTime ActionDate { get; set; }
            public string OverallStatus { get; set; } = null!;
        }

        public List<Domain.Entities.Leave.Leave> PendingLeaves { get; set; } = new();
        public List<ReviewedLeaveViewModel> ReviewedLeaves { get; set; } = new();
        public int ManagerId { get; set; }

        [TempData]
        public string? SuccessMessage { get; set; }

        [TempData]
        public string? ErrorMessage { get; set; }

        public async Task OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return;
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

            if (employee != null)
            {
                ManagerId = employee.Id;
                PendingLeaves = await _leaveService.GetPendingApprovalsAsync(ManagerId);

                // Query manager's action history
                ReviewedLeaves = await _context.LeaveApprovals
                    .Include(la => la.Leave)
                        .ThenInclude(l => l.Employee)
                    .Where(la => la.ApproverId == employee.Id)
                    .OrderByDescending(la => la.ApprovalDate)
                    .Select(la => new ReviewedLeaveViewModel
                    {
                        LeaveId = la.LeaveId,
                        EmployeeName = la.Leave.Employee.FullName,
                        LeaveType = la.Leave.LeaveType,
                        StartDate = la.Leave.StartDate,
                        EndDate = la.Leave.EndDate,
                        TotalDays = la.Leave.TotalDays,
                        ActionTaken = la.Status,
                        Comments = la.Comments,
                        ActionDate = la.ApprovalDate,
                        OverallStatus = la.Leave.Status
                    })
                    .ToListAsync();
            }
        }

        public async Task<IActionResult> OnPostApproveAsync(int leaveId, string comments)
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

            if (employee != null)
            {
                try
                {
                    await _leaveService.ApproveLeaveAsync(leaveId, employee.Id, comments);
                    SuccessMessage = "Leave approved successfully!";
                }
                catch (System.Exception ex)
                {
                    ErrorMessage = ex.Message;
                }
            }
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostRejectAsync(int leaveId, string reason)
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

            if (employee != null)
            {
                try
                {
                    await _leaveService.RejectLeaveAsync(leaveId, employee.Id, reason);
                    SuccessMessage = "Leave rejected successfully!";
                }
                catch (System.Exception ex)
                {
                    ErrorMessage = ex.Message;
                }
            }
            return RedirectToPage();
        }
    }
}
