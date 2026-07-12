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

namespace HRMS.UI.Pages.Employee.Leave
{
    [Authorize]
    public class ApplyModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly ILeaveService _leaveService;
        private readonly IOverseasLeaveService _overseasService;
        private readonly IMaternityLeaveService _maternityService;
        private readonly UserManager<ApplicationUser> _userManager;

        public ApplyModel(
            ApplicationDbContext context, 
            ILeaveService leaveService, 
            IOverseasLeaveService overseasService,
            IMaternityLeaveService maternityService,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _leaveService = leaveService;
            _overseasService = overseasService;
            _maternityService = maternityService;
            _userManager = userManager;
        }

        public int EmployeeId { get; set; }
        public string EmployeeGender { get; set; } = string.Empty;

        [TempData]
        public string? ErrorMessage { get; set; }
        
        [TempData]
        public string? SuccessMessage { get; set; }

        [BindProperty]
        public string ActiveTab { get; set; } = "standard";

        // Standard properties
        [BindProperty]
        public string LeaveType { get; set; } = "Annual";
        [BindProperty]
        public DateTime StartDate { get; set; } = DateTime.Today;
        [BindProperty]
        public DateTime EndDate { get; set; } = DateTime.Today;
        [BindProperty]
        public string? Reason { get; set; }
        public int CalculatedDays { get; set; }

        // Overseas properties
        [BindProperty]
        public DateTime OverseasStartDate { get; set; } = DateTime.Today.AddDays(30);
        [BindProperty]
        public DateTime OverseasEndDate { get; set; } = DateTime.Today.AddDays(60);
        [BindProperty]
        public string? OverseasReason { get; set; }
        [BindProperty]
        public string PassportNumber { get; set; } = string.Empty;
        [BindProperty]
        public DateTime PassportExpiry { get; set; } = DateTime.Today.AddYears(1);
        [BindProperty]
        public string Country { get; set; } = string.Empty;
        [BindProperty]
        public string? ContactDetails { get; set; }

        // Maternity properties
        [BindProperty]
        public DateTime MaternityStartDate { get; set; } = DateTime.Today;
        [BindProperty]
        public DateTime MaternityEndDate { get; set; } = DateTime.Today.AddDays(84);
        [BindProperty]
        public int ChildNumber { get; set; } = 1;
        [BindProperty]
        public DateTime? ExpectedDeliveryDate { get; set; }
        [BindProperty]
        public string? MaternityReason { get; set; }

        private async Task<Domain.Entities.Core.Employee?> GetCurrentEmployeeAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return null;

            if (user.EmployeeId.HasValue)
            {
                return await _context.Employees.FindAsync(user.EmployeeId.Value);
            }
            return await _context.Employees.FirstOrDefaultAsync(e => e.Email == user.Email);
        }

        public async Task<IActionResult> OnGet()
        {
            var employee = await GetCurrentEmployeeAsync();
            if (employee == null || employee.NIC == "DUTY-ACC")
            {
                return Forbid();
            }

            EmployeeId = employee.Id;
            EmployeeGender = employee.Sex ?? "";
            CalculatedDays = await _leaveService.CalculateLeaveDaysAsync(StartDate, EndDate);
            return Page();
        }

        public async Task<IActionResult> OnPostApplyAsync()
        {
            ActiveTab = "standard";
            var employee = await GetCurrentEmployeeAsync();
            if (employee == null || employee.NIC == "DUTY-ACC") return Forbid();

            EmployeeId = employee.Id;
            EmployeeGender = employee.Sex ?? "";
            CalculatedDays = await _leaveService.CalculateLeaveDaysAsync(StartDate, EndDate);

            if (StartDate > EndDate)
            {
                ErrorMessage = "End date must be after start date";
                return Page();
            }

            if (LeaveType == "Maternity" && EmployeeGender.Equals("Male", StringComparison.OrdinalIgnoreCase))
            {
                ErrorMessage = "Male employees are not eligible for Maternity Leave.";
                return Page();
            }

            try
            {
                var leave = new Domain.Entities.Leave.Leave
                {
                    EmployeeId = EmployeeId,
                    LeaveType = LeaveType,
                    StartDate = StartDate,
                    EndDate = EndDate,
                    TotalDays = CalculatedDays,
                    Reason = Reason,
                    Status = "Pending"
                };

                await _leaveService.ApplyLeaveAsync(leave);
                SuccessMessage = "Leave application submitted successfully!";
                return RedirectToPage("./Status");
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }

            return Page();
        }

        public async Task<IActionResult> OnPostOverseasAsync()
        {
            ActiveTab = "overseas";
            var employee = await GetCurrentEmployeeAsync();
            if (employee == null || employee.NIC == "DUTY-ACC") return Forbid();

            EmployeeId = employee.Id;
            EmployeeGender = employee.Sex ?? "";

            if (OverseasStartDate > OverseasEndDate)
            {
                ErrorMessage = "End date must be after start date";
                return Page();
            }

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
                    StartDate = OverseasStartDate,
                    EndDate = OverseasEndDate,
                    Reason = OverseasReason
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

        public async Task<IActionResult> OnPostMaternityAsync()
        {
            ActiveTab = "maternity";
            var employee = await GetCurrentEmployeeAsync();
            if (employee == null || employee.NIC == "DUTY-ACC") return Forbid();

            EmployeeId = employee.Id;
            EmployeeGender = employee.Sex ?? "";

            if (EmployeeGender.Equals("Male", StringComparison.OrdinalIgnoreCase))
            {
                ErrorMessage = "Male employees are not eligible for Maternity Leave.";
                return Page();
            }

            if (MaternityStartDate > MaternityEndDate)
            {
                ErrorMessage = "End date must be after start date";
                return Page();
            }

            try
            {
                var leave = new Domain.Entities.Leave.Leave
                {
                    EmployeeId = EmployeeId,
                    StartDate = MaternityStartDate,
                    EndDate = MaternityEndDate,
                    Reason = MaternityReason
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
