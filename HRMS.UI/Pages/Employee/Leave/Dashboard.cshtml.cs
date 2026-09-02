using System;
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

namespace HRMS.UI.Pages.Employee.Leave
{
    [Authorize]
    public class DashboardModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly ILeaveService _leaveService;
        private readonly UserManager<ApplicationUser> _userManager;

        public DashboardModel(ApplicationDbContext context, ILeaveService leaveService, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _leaveService = leaveService;
            _userManager = userManager;
        }

        public int EmployeeId { get; set; }
        public string EmployeeGender { get; set; } = "";
        public List<LeaveEntitlement> LeaveBalances { get; set; } = new();
        public List<Domain.Entities.Leave.Leave> MyLeaves { get; set; } = new();
        public int PendingCount { get; set; }
        public int UsedCount { get; set; }
        public List<Domain.Entities.Leave.Leave> UpcomingApprovedLeaves { get; set; } = new();

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
            var currentYear = DateTime.Now.Year;

            var balances = await _leaveService.GetAllLeaveBalancesAsync(EmployeeId, currentYear);
            if (employee.Sex != null && employee.Sex.Equals("Male", StringComparison.OrdinalIgnoreCase))
            {
                LeaveBalances = balances.Where(b => !b.LeaveType.Equals("Maternity", StringComparison.OrdinalIgnoreCase)).ToList();
            }
            else
            {
                LeaveBalances = balances;
            }
            MyLeaves = await _leaveService.GetEmployeeLeavesAsync(EmployeeId);
            PendingCount = MyLeaves.Count(l => l.Status != null && l.Status.StartsWith("Pending"));
            UsedCount = MyLeaves.Count(l => l.Status == "Approved");
            UpcomingApprovedLeaves = MyLeaves
                .Where(l => l.Status == "Approved" && l.StartDate > DateTime.Now)
                .OrderBy(l => l.StartDate)
                .Take(5)
                .ToList();

            return Page();
        }
    }
}
