using System.Collections.Generic;
using System.Linq;
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
    public class ReviewModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly ILeaveService _leaveService;
        private readonly UserManager<ApplicationUser> _userManager;

        public ReviewModel(ApplicationDbContext context, ILeaveService leaveService, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _leaveService = leaveService;
            _userManager = userManager;
        }

        public Domain.Entities.Leave.Leave LeaveItem { get; set; } = null!;
        public List<Domain.Entities.Leave.Leave> LeaveHistory { get; set; } = new();
        public List<LeaveEntitlement> LeaveBalances { get; set; } = new();
        public bool CanApprove { get; set; }
        public string ApplicantWorkflowRole { get; set; } = "Employee";
        public List<LeaveApproval> ApprovalSteps { get; set; } = new();

        [TempData]
        public string? SuccessMessage { get; set; }

        [TempData]
        public string? ErrorMessage { get; set; }

        public async Task<IActionResult> OnGetAsync(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Forbid();
            }

            var leave = await _context.Leaves
                .Include(l => l.Employee)
                    .ThenInclude(e => e.Department)
                .Include(l => l.Employee)
                    .ThenInclude(e => e.Branch)
                .Include(l => l.Employee)
                    .ThenInclude(e => e.Designation)
                .Include(l => l.MaternityLeave)
                .Include(l => l.OverseasLeave)
                .FirstOrDefaultAsync(l => l.Id == id);

            if (leave == null)
            {
                return NotFound();
            }

            LeaveItem = leave;

            if (leave.Employee != null)
            {
                ApplicantWorkflowRole = await _leaveService.GetApplicantWorkflowRoleAsync(leave.Employee);
            }

            // Fetch approval timeline steps
            ApprovalSteps = await _context.LeaveApprovals
                .Include(la => la.Approver)
                .Where(la => la.LeaveId == id)
                .OrderBy(la => la.ApprovalDate)
                .ToListAsync();

            // Fetch employee leave balances for the year of the leave request
            var balances = await _leaveService.GetAllLeaveBalancesAsync(leave.EmployeeId, leave.StartDate.Year);
            if (leave.Employee?.Sex != null && leave.Employee.Sex.Equals("Male", StringComparison.OrdinalIgnoreCase))
            {
                LeaveBalances = balances.Where(b => !b.LeaveType.Equals("Maternity", StringComparison.OrdinalIgnoreCase)).ToList();
            }
            else
            {
                LeaveBalances = balances;
            }

            // Fetch employee past leave history (excluding current request)
            LeaveHistory = await _context.Leaves
                .Include(l => l.ApprovedBy)
                .Where(l => l.EmployeeId == leave.EmployeeId && l.Id != leave.Id)
                .OrderByDescending(l => l.StartDate)
                .ToListAsync();

            // Resolve manager employee and user roles to check if they can approve
            CanApprove = false;
            Domain.Entities.Core.Employee? managerEmp = null;
            if (user.EmployeeId.HasValue)
            {
                managerEmp = await _context.Employees.FindAsync(user.EmployeeId.Value);
            }
            else
            {
                managerEmp = await _context.Employees.FirstOrDefaultAsync(e => e.Email == user.Email);
            }

            var userRoles = await (from ur in _context.UserRoles
                                   join r in _context.Roles on ur.RoleId equals r.Id
                                   where ur.UserId == user.Id
                                   select r.Name)
                                  .ToListAsync();

            int approverBranchId = managerEmp?.BranchId ?? 0;
            string? approverBranchName = user.Branch ?? managerEmp?.Branch?.Name;
            if (approverBranchId == 0 && !string.IsNullOrEmpty(approverBranchName))
            {
                var b = await _context.Branches.FirstOrDefaultAsync(br => br.Name == approverBranchName);
                if (b != null) approverBranchId = b.Id;
            }

            int approverDeptId = managerEmp?.DepartmentId ?? 0;
            string? approverDeptName = user.Department ?? managerEmp?.Department?.Name;
            if (approverDeptId == 0 && !string.IsNullOrEmpty(approverDeptName))
            {
                var d = await _context.Departments.FirstOrDefaultAsync(dp => dp.Name == approverDeptName);
                if (d != null) approverDeptId = d.Id;
            }

            List<int> managedBranchIds = new();
            if (!string.IsNullOrEmpty(user.ManagedBranches))
            {
                managedBranchIds = user.ManagedBranches.Split(',')
                    .Select(s => int.TryParse(s.Trim(), out var bid) ? bid : 0)
                    .Where(bid => bid > 0)
                    .ToList();
            }

            if (userRoles.Contains("Admin"))
            {
                CanApprove = leave.Status.StartsWith("Pending");
            }
            else if (leave.Status == "Pending" || leave.Status == "PendingDH")
            {
                CanApprove = userRoles.Contains("Department Head") &&
                    ((approverBranchId > 0 && leave.Employee?.BranchId == approverBranchId) || (!string.IsNullOrEmpty(approverBranchName) && leave.Employee?.Branch?.Name == approverBranchName)) &&
                    ((approverDeptId > 0 && leave.Employee?.DepartmentId == approverDeptId) || (!string.IsNullOrEmpty(approverDeptName) && leave.Employee?.Department?.Name == approverDeptName));
            }
            else if (leave.Status == "PendingBM")
            {
                CanApprove = userRoles.Contains("Branch Manager") &&
                    ((approverBranchId > 0 && leave.Employee?.BranchId == approverBranchId) || (!string.IsNullOrEmpty(approverBranchName) && leave.Employee?.Branch?.Name == approverBranchName));
            }
            else if (leave.Status == "PendingAM")
            {
                CanApprove = userRoles.Contains("Area Manager") &&
                    (managedBranchIds.Any() ? (leave.Employee?.BranchId != null && managedBranchIds.Contains(leave.Employee.BranchId)) : (approverBranchId == 0 || leave.Employee?.BranchId == approverBranchId || leave.Employee?.Branch?.Name == approverBranchName));
            }
            else if (leave.Status == "PendingHR")
            {
                CanApprove = userRoles.Contains("HR Manager") || userRoles.Contains("HR Officer");
            }

            return Page();
        }

        public async Task<IActionResult> OnPostApproveAsync(int id, string comments)
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

            if (employee == null)
            {
                ErrorMessage = "Unable to resolve your manager profile.";
                return RedirectToPage(new { id });
            }

            try
            {
                var leave = await _context.Leaves.FindAsync(id);
                string tab = leave?.LeaveType?.Equals("Maternity", StringComparison.OrdinalIgnoreCase) == true ? "maternity" 
                           : leave?.LeaveType?.Equals("Overseas", StringComparison.OrdinalIgnoreCase) == true ? "overseas" : "standard";

                await _leaveService.ApproveLeaveAsync(id, employee.Id, comments);
                SuccessMessage = "Leave approved successfully!";
                return RedirectToPage("/Manager/Leave/Approval", new { tab });
            }
            catch (System.Exception ex)
            {
                ErrorMessage = ex.Message;
                return RedirectToPage(new { id });
            }
        }

        public async Task<IActionResult> OnPostRejectAsync(int id, string reason)
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

            if (employee == null)
            {
                ErrorMessage = "Unable to resolve your manager profile.";
                return RedirectToPage(new { id });
            }

            try
            {
                var leave = await _context.Leaves.FindAsync(id);
                string tab = leave?.LeaveType?.Equals("Maternity", StringComparison.OrdinalIgnoreCase) == true ? "maternity" 
                           : leave?.LeaveType?.Equals("Overseas", StringComparison.OrdinalIgnoreCase) == true ? "overseas" : "standard";

                await _leaveService.RejectLeaveAsync(id, employee.Id, reason);
                SuccessMessage = "Leave rejected successfully!";
                return RedirectToPage("/Manager/Leave/Approval", new { tab });
            }
            catch (System.Exception ex)
            {
                ErrorMessage = ex.Message;
                return RedirectToPage(new { id });
            }
        }
    }
}
