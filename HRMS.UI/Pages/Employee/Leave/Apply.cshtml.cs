using System;
using System.IO;
using System.Threading.Tasks;
using HRMS.Domain.Entities.Leave;
using HRMS.Infrastructure.Identity;
using HRMS.Infrastructure.Persistence;
using HRMS.UI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
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
        private readonly IWebHostEnvironment _webHostEnvironment;

        public ApplyModel(
            ApplicationDbContext context, 
            ILeaveService leaveService, 
            IOverseasLeaveService overseasService,
            IMaternityLeaveService maternityService,
            UserManager<ApplicationUser> userManager,
            IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _leaveService = leaveService;
            _overseasService = overseasService;
            _maternityService = maternityService;
            _userManager = userManager;
            _webHostEnvironment = webHostEnvironment;
        }

        public int EmployeeId { get; set; }
        public string EmployeeGender { get; set; } = string.Empty;
        public List<LeaveEntitlement> LeaveBalances { get; set; } = new();

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
        public bool IsHalfDay { get; set; } = false;
        [BindProperty]
        public string? HalfDaySession { get; set; } = "First Half (Morning)";
        [BindProperty]
        public DateTime StartDate { get; set; } = DateTime.Today;
        [BindProperty]
        public DateTime EndDate { get; set; } = DateTime.Today;
        [BindProperty]
        public string? Reason { get; set; }
        public double CalculatedDays { get; set; }
        [BindProperty]
        public IFormFile? StandardAttachmentFile { get; set; }

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
        [BindProperty]
        public IFormFile? PassportCopyFile { get; set; }
        [BindProperty]
        public IFormFile? ConfirmationLetterFile { get; set; }

        // Maternity properties
        [BindProperty]
        public DateTime MaternityStartDate { get; set; } = DateTime.Today;
        [BindProperty]
        public DateTime MaternityEndDate { get; set; } = DateTime.Today.AddDays(84);
        [BindProperty]
        public DateTime? ExpectedDeliveryDate { get; set; } = DateTime.Today.AddDays(14);
        [BindProperty]
        public int ChildNumber { get; set; } = 1;
        [BindProperty]
        public string? MaternityReason { get; set; }
        [BindProperty]
        public IFormFile? MedicalCertificateFile { get; set; }
        [BindProperty]
        public IFormFile? DoctorLetterFile { get; set; }

        private async Task<string?> SaveUploadedFileAsync(IFormFile? file, string subfolder)
        {
            if (file == null || file.Length == 0) return null;

            var uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", subfolder);
            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            var uniqueFileName = $"{Guid.NewGuid()}_{Path.GetFileName(file.FileName)}";
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }
            return $"/uploads/{subfolder}/{uniqueFileName}";
        }

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
            LeaveBalances = await _leaveService.GetAllLeaveBalancesAsync(EmployeeId, DateTime.Now.Year);
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
            LeaveBalances = await _leaveService.GetAllLeaveBalancesAsync(EmployeeId, DateTime.Now.Year);

            if (IsHalfDay && (LeaveType == "Casual" || LeaveType == "Annual"))
            {
                EndDate = StartDate.Date;
                CalculatedDays = 0.5;

                if (StartDate.Date < DateTime.Today.AddDays(-2))
                {
                    ErrorMessage = "Leave start date cannot be more than 2 days in the past.";
                    return Page();
                }

                if (StartDate.DayOfWeek == DayOfWeek.Saturday || StartDate.DayOfWeek == DayOfWeek.Sunday)
                {
                    ErrorMessage = "Half-day leave cannot be applied on weekends.";
                    return Page();
                }

                if (string.IsNullOrWhiteSpace(HalfDaySession))
                {
                    HalfDaySession = "First Half (Morning)";
                }
            }
            else
            {
                IsHalfDay = false;
                HalfDaySession = null;
                CalculatedDays = await _leaveService.CalculateLeaveDaysAsync(StartDate, EndDate);

                if (StartDate.Date < DateTime.Today.AddDays(-2))
                {
                    ErrorMessage = "Leave start date cannot be more than 2 days in the past.";
                    return Page();
                }

                if (StartDate.Date > EndDate.Date)
                {
                    ErrorMessage = "End date cannot be earlier than start date.";
                    return Page();
                }

                if (CalculatedDays <= 0)
                {
                    ErrorMessage = "The selected date range does not contain any working days (weekends are excluded).";
                    return Page();
                }
            }

            if (LeaveType == "Maternity" && EmployeeGender.Equals("Male", StringComparison.OrdinalIgnoreCase))
            {
                ErrorMessage = "Male employees are not eligible for Maternity Leave.";
                return Page();
            }

            try
            {
                string? attachmentPath = await SaveUploadedFileAsync(StandardAttachmentFile, "standard");

                var leave = new Domain.Entities.Leave.Leave
                {
                    EmployeeId = EmployeeId,
                    LeaveType = LeaveType,
                    StartDate = StartDate,
                    EndDate = EndDate,
                    TotalDays = CalculatedDays,
                    IsHalfDay = IsHalfDay && (LeaveType == "Casual" || LeaveType == "Annual"),
                    HalfDaySession = (IsHalfDay && (LeaveType == "Casual" || LeaveType == "Annual")) ? HalfDaySession : null,
                    Reason = Reason,
                    AttachmentPath = attachmentPath,
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
            LeaveBalances = await _leaveService.GetAllLeaveBalancesAsync(EmployeeId, DateTime.Now.Year);

            if (OverseasStartDate.Date < DateTime.Today.AddDays(-2))
            {
                ErrorMessage = "Leave start date cannot be more than 2 days in the past.";
                return Page();
            }

            if (OverseasStartDate.Date > OverseasEndDate.Date)
            {
                ErrorMessage = "End date cannot be earlier than start date.";
                return Page();
            }

            if (PassportExpiry.Date <= OverseasEndDate.Date)
            {
                ErrorMessage = "Passport must be valid until at least after the overseas leave end date.";
                return Page();
            }

            if (PassportCopyFile == null || PassportCopyFile.Length == 0)
            {
                ErrorMessage = "Please attach a Passport Bio / Visa document copy.";
                return Page();
            }

            try
            {
                string? passportCopyPath = await SaveUploadedFileAsync(PassportCopyFile, "overseas");
                string? confirmationPath = await SaveUploadedFileAsync(ConfirmationLetterFile, "overseas");

                var leave = new Domain.Entities.Leave.Leave
                {
                    EmployeeId = EmployeeId,
                    StartDate = OverseasStartDate,
                    EndDate = OverseasEndDate,
                    Reason = OverseasReason,
                    AttachmentPath = passportCopyPath
                };

                var overseasDetails = new OverseasLeave
                {
                    PassportNumber = PassportNumber,
                    PassportExpiry = PassportExpiry,
                    Country = Country,
                    ContactDetailsOverseas = ContactDetails,
                    PassportCopyPath = passportCopyPath,
                    ConfirmationLetterPath = confirmationPath
                };

                await _overseasService.SubmitOverseasLeaveAsync(leave, overseasDetails);
                SuccessMessage = "Overseas leave request submitted successfully! Forwarded to Branch Manager for approval.";
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
            LeaveBalances = await _leaveService.GetAllLeaveBalancesAsync(EmployeeId, DateTime.Now.Year);

            if (EmployeeGender.Equals("Male", StringComparison.OrdinalIgnoreCase))
            {
                ErrorMessage = "Male employees are not eligible for Maternity Leave.";
                return Page();
            }

            if (MaternityStartDate.Date < DateTime.Today.AddDays(-2))
            {
                ErrorMessage = "Leave start date cannot be more than 2 days in the past.";
                return Page();
            }

            if (MaternityStartDate.Date > MaternityEndDate.Date)
            {
                ErrorMessage = "End date cannot be earlier than start date.";
                return Page();
            }

            if (MedicalCertificateFile == null || MedicalCertificateFile.Length == 0)
            {
                ErrorMessage = "Please attach a Medical Certificate for maternity leave.";
                return Page();
            }

            try
            {
                string? mcPath = await SaveUploadedFileAsync(MedicalCertificateFile, "maternity");
                string? dlPath = await SaveUploadedFileAsync(DoctorLetterFile, "maternity");

                var leave = new Domain.Entities.Leave.Leave
                {
                    EmployeeId = EmployeeId,
                    StartDate = MaternityStartDate,
                    EndDate = MaternityEndDate,
                    Reason = MaternityReason,
                    AttachmentPath = mcPath
                };

                var maternityDetails = new MaternityLeave
                {
                    ChildNumber = ChildNumber,
                    ExpectedDeliveryDate = ExpectedDeliveryDate,
                    MedicalCertificate = MedicalCertificateFile.FileName,
                    MedicalCertificatePath = mcPath,
                    DoctorLetterPath = dlPath
                };

                await _maternityService.SubmitMaternityLeaveAsync(leave, maternityDetails);
                SuccessMessage = "Maternity leave request submitted successfully! Forwarded to Branch Manager for approval.";
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
