using System;
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

namespace HRMS.UI.Pages.Employee.Maternity
{
    [Authorize]
    public class RequestModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly IMaternityLeaveService _maternityService;
        private readonly UserManager<ApplicationUser> _userManager;

        public RequestModel(ApplicationDbContext context, IMaternityLeaveService maternityService, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _maternityService = maternityService;
            _userManager = userManager;
        }

        public int EmployeeId { get; set; }

        [TempData]
        public string? ErrorMessage { get; set; }

        [TempData]
        public string? SuccessMessage { get; set; }

        [BindProperty]
        public DateTime StartDate { get; set; } = DateTime.Today;

        [BindProperty]
        public DateTime EndDate { get; set; } = DateTime.Today.AddDays(84);

        [BindProperty]
        public int ChildNumber { get; set; } = 1;

        [BindProperty]
        public DateTime? ExpectedDeliveryDate { get; set; }

        [BindProperty]
        public string? Reason { get; set; }

        public async Task<IActionResult> OnGet()
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

            if (employee.Sex.Equals("Male", StringComparison.OrdinalIgnoreCase))
            {
                ErrorMessage = "Male employees are not eligible for Maternity Leave.";
                return RedirectToPage("/Employee/Leave/Dashboard");
            }

            EmployeeId = employee.Id;
            return Page();
        }

        public async Task<IActionResult> OnPost()
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

            if (employee.Sex.Equals("Male", StringComparison.OrdinalIgnoreCase))
            {
                ErrorMessage = "Male employees are not eligible for Maternity Leave.";
                return RedirectToPage("/Employee/Leave/Dashboard");
            }

            EmployeeId = employee.Id;

            try
            {
                var leave = new Domain.Entities.Leave.Leave
                {
                    EmployeeId = EmployeeId,
                    StartDate = StartDate,
                    EndDate = EndDate,
                    Reason = Reason
                };

                var maternityDetails = new MaternityLeave
                {
                    ChildNumber = ChildNumber,
                    ExpectedDeliveryDate = ExpectedDeliveryDate
                };

                await _maternityService.SubmitMaternityLeaveAsync(leave, maternityDetails);
                SuccessMessage = "Maternity leave request submitted successfully!";
                return RedirectToPage("./Status");
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }

            return Page();
        }
    }
}
