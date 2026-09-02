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
    [Authorize(Roles = "Department Head, Branch Manager, Area Manager, HR Manager, HR Officer, Admin")]
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
            public bool IsHalfDay { get; set; }
            public string? HalfDaySession { get; set; }
            public DateTime StartDate { get; set; }
            public DateTime EndDate { get; set; }
            public double TotalDays { get; set; }
            public string ActionTaken { get; set; } = null!; // Approved / Rejected
            public string? Comments { get; set; }
            public DateTime ActionDate { get; set; }
            public string OverallStatus { get; set; } = null!;
            public string? AdditionalInfo { get; set; }
        }

        [BindProperty(SupportsGet = true)]
        public string Tab { get; set; } = "standard";

        public List<Domain.Entities.Leave.Leave> PendingLeaves { get; set; } = new();
        public List<Domain.Entities.Leave.Leave> PendingStandardLeaves { get; set; } = new();
        public List<Domain.Entities.Leave.Leave> PendingMaternityLeaves { get; set; } = new();
        public List<Domain.Entities.Leave.Leave> PendingOverseasLeaves { get; set; } = new();

        public List<ReviewedLeaveViewModel> ReviewedLeaves { get; set; } = new();
        public List<ReviewedLeaveViewModel> ReviewedStandardLeaves { get; set; } = new();
        public List<ReviewedLeaveViewModel> ReviewedMaternityLeaves { get; set; } = new();
        public List<ReviewedLeaveViewModel> ReviewedOverseasLeaves { get; set; } = new();

        public int StandardCount => PendingStandardLeaves.Count;
        public int MaternityCount => PendingMaternityLeaves.Count;
        public int OverseasCount => PendingOverseasLeaves.Count;
        public int TotalPendingCount => PendingLeaves.Count;

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

                // Categorize Pending Leaves
                PendingStandardLeaves = PendingLeaves
                    .Where(l => !string.Equals(l.LeaveType, "Maternity", StringComparison.OrdinalIgnoreCase) && 
                                !string.Equals(l.LeaveType, "Overseas", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                PendingMaternityLeaves = PendingLeaves
                    .Where(l => string.Equals(l.LeaveType, "Maternity", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                PendingOverseasLeaves = PendingLeaves
                    .Where(l => string.Equals(l.LeaveType, "Overseas", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                // Query manager's action history
                var approvalsRaw = await _context.LeaveApprovals
                    .Include(la => la.Leave)
                        .ThenInclude(l => l.Employee)
                    .Include(la => la.Leave)
                        .ThenInclude(l => l.MaternityLeave)
                    .Include(la => la.Leave)
                        .ThenInclude(l => l.OverseasLeave)
                    .Where(la => la.ApproverId == employee.Id)
                    .OrderByDescending(la => la.ApprovalDate)
                    .ToListAsync();

                ReviewedLeaves = approvalsRaw.Select(la => new ReviewedLeaveViewModel
                {
                    LeaveId = la.LeaveId,
                    EmployeeName = la.Leave?.Employee != null ? la.Leave.Employee.NameWithInitials : "Unknown",
                    LeaveType = la.Leave?.LeaveType ?? "",
                    IsHalfDay = la.Leave?.IsHalfDay ?? false,
                    HalfDaySession = la.Leave?.HalfDaySession,
                    StartDate = la.Leave?.StartDate ?? DateTime.MinValue,
                    EndDate = la.Leave?.EndDate ?? DateTime.MinValue,
                    TotalDays = la.Leave?.TotalDays ?? 0,
                    ActionTaken = la.Status,
                    Comments = la.Comments,
                    ActionDate = la.ApprovalDate,
                    OverallStatus = la.Leave?.Status ?? "",
                    AdditionalInfo = la.Leave?.LeaveType == "Overseas" && la.Leave?.OverseasLeave != null 
                        ? $"Destination: {la.Leave.OverseasLeave.Country}" 
                        : (la.Leave?.LeaveType == "Maternity" && la.Leave?.MaternityLeave != null 
                            ? $"Child #{la.Leave.MaternityLeave.ChildNumber}" 
                            : null)
                }).ToList();

                // Categorize Reviewed History
                ReviewedStandardLeaves = ReviewedLeaves
                    .Where(r => !string.Equals(r.LeaveType, "Maternity", StringComparison.OrdinalIgnoreCase) && 
                                !string.Equals(r.LeaveType, "Overseas", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                ReviewedMaternityLeaves = ReviewedLeaves
                    .Where(r => string.Equals(r.LeaveType, "Maternity", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                ReviewedOverseasLeaves = ReviewedLeaves
                    .Where(r => string.Equals(r.LeaveType, "Overseas", StringComparison.OrdinalIgnoreCase))
                    .ToList();
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
