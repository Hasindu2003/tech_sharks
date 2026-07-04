using HRMS.Application.Leave;
using HRMS.Domain.Entities.Leave;
using HRMS.Infrastructure.Identity;
using HRMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace HRMS.UI.Pages.Settings.LeaveBalances
{
    [Authorize(Roles = "Admin,HR Manager")]
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly ILeaveService _leaveService;
        private readonly UserManager<ApplicationUser> _userManager;

        public IndexModel(ILeaveService leaveService, ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _leaveService = leaveService;
            _context = context;
            _userManager = userManager;
        }

        [BindProperty(SupportsGet = true)] public int? EmployeeId { get; set; }

        [BindProperty(SupportsGet = true)] public int Year { get; set; } = DateTime.Today.Year;

        public SelectList EmployeeList { get; set; } = default!;
        public List<LeaveBalanceDto> Balances { get; set; } = new();

        public async Task OnGetAsync()
        {
            await LoadEmployeesAsync();

            if (EmployeeId.HasValue)
                Balances = await _leaveService.GetBalancesAsync(EmployeeId.Value, Year);
        }

        public async Task<IActionResult> OnPostAdjustAsync(int employeeId, int year, LeaveType leaveType,
            decimal deltaDays, string reason)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user?.EmployeeId == null)
            {
                TempData["ErrorMessage"] = "Your account is not linked to an employee profile.";
                return RedirectToPage(new { EmployeeId = employeeId, Year = year });
            }

            var result = await _leaveService.AdjustBalanceAsync(employeeId, leaveType, year, deltaDays, reason,
                user.EmployeeId.Value);
            TempData[result.Success ? "SuccessMessage" : "ErrorMessage"] = result.Message;
            return RedirectToPage(new { EmployeeId = employeeId, Year = year });
        }

        private async Task LoadEmployeesAsync()
        {
            var employees = await _context.Employees.OrderBy(e => e.FirstName).ToListAsync();
            EmployeeList = new SelectList(
                employees.Select(e => new { e.Id, Name = $"{e.FirstName} {e.LastName}" }),
                "Id", "Name", EmployeeId);
        }
    }
}
