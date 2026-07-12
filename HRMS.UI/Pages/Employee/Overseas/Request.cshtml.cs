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

namespace HRMS.UI.Pages.Employee.Overseas
{
    [Authorize]
    public class RequestModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly IOverseasLeaveService _overseasService;
        private readonly UserManager<ApplicationUser> _userManager;

        public RequestModel(ApplicationDbContext context, IOverseasLeaveService overseasService, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _overseasService = overseasService;
            _userManager = userManager;
        }

        public int EmployeeId { get; set; }
        
        [TempData]
        public string? ErrorMessage { get; set; }
        
        [TempData]
        public string? SuccessMessage { get; set; }

        [BindProperty]
        public DateTime StartDate { get; set; } = DateTime.Today.AddDays(30);

        [BindProperty]
        public DateTime EndDate { get; set; } = DateTime.Today.AddDays(60);

        [BindProperty]
        public string? Reason { get; set; }

        [BindProperty]
        public string PassportNumber { get; set; } = string.Empty;

        [BindProperty]
        public DateTime PassportExpiry { get; set; } = DateTime.Today.AddYears(1);

        [BindProperty]
        public string Country { get; set; } = string.Empty;

        [BindProperty]
        public string? ContactDetails { get; set; }

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

            EmployeeId = employee.Id;

            if (PassportExpiry <= DateTime.Now)
            {
                ErrorMessage = "Passport has expired. Please renew your passport.";
                return Page();
            }

            try
            {
                var leave = new Domain.Entities.Leave.Leave
                {
                    EmployeeId = EmployeeId,
                    StartDate = StartDate,
                    EndDate = EndDate,
                    Reason = Reason
                };

                var overseasDetails = new OverseasLeave
                {
                    PassportNumber = PassportNumber,
                    PassportExpiry = PassportExpiry,
                    Country = Country,
                    ContactDetailsOverseas = ContactDetails
                };

                await _overseasService.SubmitOverseasLeaveAsync(leave, overseasDetails);
                SuccessMessage = "Overseas leave request submitted successfully!";
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
