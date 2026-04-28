using HRMS.Domain.Entities.Termination;
using HRMS.Infrastructure.Identity;
using HRMS.Infrastructure.Persistence;
using HRMS.UI.Models;
using HRMS.UI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace HRMS.UI.Pages.Termination
{
    [Authorize(Roles = "HR Manager")]
    public class CreateRequestModel : PageModel
    {
        private readonly ITerminationService _terminationService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;

        public CreateRequestModel(ITerminationService terminationService, UserManager<ApplicationUser> userManager, ApplicationDbContext context)
        {
            _terminationService = terminationService;
            _userManager = userManager;
            _context = context;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public List<EmployeeOption> Employees { get; set; } = new();

        public class EmployeeOption
        {
            public string Email { get; set; } = string.Empty;
            public string FullName { get; set; } = string.Empty;
            public string EpfNumber { get; set; } = string.Empty;
            public string Branch { get; set; } = string.Empty;
            public string Department { get; set; } = string.Empty;
            public string Designation { get; set; } = string.Empty;
        }

        public class InputModel
        {
            [Required(ErrorMessage = "Please select an employee.")]
            public string SelectedEmployeeEmail { get; set; } = string.Empty;

            [Required(ErrorMessage = "Termination type is required.")]
            [Display(Name = "Termination Type")]
            public TerminationTypeEnum TerminationType { get; set; }

            [Required(ErrorMessage = "Reason for termination is required.")]
            [StringLength(1000, MinimumLength = 10, ErrorMessage = "Reason must be between 10 and 1000 characters.")]
            [Display(Name = "Reason for Termination")]
            public string ReasonForTermination { get; set; } = string.Empty;

            [Required(ErrorMessage = "Initiation date is required.")]
            [DataType(DataType.Date)]
            [Display(Name = "Initiation Date")]
            public DateTime? InitiationDate { get; set; }

            [Required(ErrorMessage = "Effective termination date is required.")]
            [DataType(DataType.Date)]
            [Display(Name = "Effective Termination Date")]
            public DateTime? EffectiveTerminationDate { get; set; }

            [StringLength(1000)]
            [Display(Name = "Supervisor Remarks")]
            public string? SupervisorRemarks { get; set; }

            [StringLength(1000)]
            [Display(Name = "Special Remarks / Notes")]
            public string? SpecialRemarks { get; set; }

            [StringLength(2000)]
            [Display(Name = "Direct Obligations")]
            public string? DirectObligations { get; set; }

            [StringLength(2000)]
            [Display(Name = "Indirect Obligations")]
            public string? IndirectObligations { get; set; }

            public bool HasOutstandingLoans { get; set; }
            public bool IsLoanGuarantor { get; set; }
            public bool HasOverridePermission { get; set; }

            [Display(Name = "Supporting Documents")]
            public List<IFormFile>? Documents { get; set; }

            public string DocumentType { get; set; } = "Other";
        }

        public async Task<IActionResult> OnGetAsync()
        {
            await PopulateEmployeesAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostSaveDraftAsync()
        {
            await PopulateEmployeesAsync();

            // Relax validations for draft
            ModelState.Remove("Input.ReasonForTermination");

            if (!ModelState.IsValid)
                return Page();

            var emp = await GetSelectedEmployeeAsync();
            if (emp == null)
            {
                ModelState.AddModelError("Input.SelectedEmployeeEmail", "Selected employee not found.");
                return Page();
            }

            if (!ValidateDates())
                return Page();

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var request = BuildViewModel(emp, user);

            var id = await _terminationService.CreateTerminationRequestAsync(request);

            // Upload documents
            await UploadDocumentsAsync(id);

            TempData["SuccessMessage"] = "Termination request saved as draft successfully.";
            return RedirectToPage("/Termination/Requests");
        }

        public async Task<IActionResult> OnPostSubmitAsync()
        {
            await PopulateEmployeesAsync();

            if (!ModelState.IsValid)
                return Page();

            var emp = await GetSelectedEmployeeAsync();
            if (emp == null)
            {
                ModelState.AddModelError("Input.SelectedEmployeeEmail", "Selected employee not found.");
                return Page();
            }

            if (!ValidateDates())
                return Page();

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var request = BuildViewModel(emp, user);

            var id = await _terminationService.CreateTerminationRequestAsync(request);

            // Upload documents
            await UploadDocumentsAsync(id);

            // Validate and submit
            var (success, error) = await _terminationService.ValidateAndSubmitAsync(id);
            if (!success)
            {
                TempData["ErrorMessage"] = error;
                return RedirectToPage("/Termination/EditRequest", new { id });
            }

            TempData["SuccessMessage"] = "Termination request submitted for approval successfully.";
            return RedirectToPage("/Termination/Requests");
        }

        private async Task PopulateEmployeesAsync()
        {
            var employeeUsers = await _userManager.GetUsersInRoleAsync("Employee");
            Employees = employeeUsers.Select(u => new EmployeeOption
            {
                Email = u.Email!,
                FullName = u.FullName,
                EpfNumber = u.EpfNumber,
                Branch = u.Branch,
                Department = u.Department ?? "",
                Designation = u.Designation
            }).OrderBy(e => e.FullName).ToList();
        }

        private async Task<ApplicationUser?> GetSelectedEmployeeAsync()
        {
            return await _userManager.FindByEmailAsync(Input.SelectedEmployeeEmail);
        }

        private bool ValidateDates()
        {
            if (Input.EffectiveTerminationDate.HasValue && Input.InitiationDate.HasValue)
            {
                if (Input.EffectiveTerminationDate.Value < Input.InitiationDate.Value)
                {
                    ModelState.AddModelError("Input.EffectiveTerminationDate", "Effective termination date cannot be before the initiation date.");
                    return false;
                }
            }
            return true;
        }

        private TerminationRequestViewModel BuildViewModel(ApplicationUser emp, ApplicationUser user)
        {
            return new TerminationRequestViewModel
            {
                EmployeeName = emp.FullName,
                EpfNumber = emp.EpfNumber,
                EmployeeEmail = emp.Email!,
                Branch = emp.Branch,
                Department = emp.Department ?? "",
                Designation = emp.Designation,
                TerminationType = Input.TerminationType,
                ReasonForTermination = Input.ReasonForTermination ?? "",
                InitiationDate = Input.InitiationDate ?? DateTime.Today,
                EffectiveTerminationDate = Input.EffectiveTerminationDate ?? DateTime.Today,
                SupervisorRemarks = Input.SupervisorRemarks,
                SpecialRemarks = Input.SpecialRemarks,
                DirectObligations = Input.DirectObligations,
                IndirectObligations = Input.IndirectObligations,
                HasOutstandingLoans = Input.HasOutstandingLoans,
                IsLoanGuarantor = Input.IsLoanGuarantor,
                HasOverridePermission = Input.HasOverridePermission,
                InitiatedBy = user.Email!,
                InitiatedByRole = "HR Manager"
            };
        }

        private async Task UploadDocumentsAsync(int requestId)
        {
            if (Input.Documents == null || !Input.Documents.Any()) return;

            var allowedExtensions = new[] { ".pdf", ".doc", ".docx", ".jpg", ".jpeg", ".png" };

            foreach (var file in Input.Documents)
            {
                if (file.Length > 5 * 1024 * 1024) continue; // Skip files over 5MB
                var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
                if (!allowedExtensions.Contains(ext)) continue;

                using var ms = new MemoryStream();
                await file.CopyToAsync(ms);

                var docType = Enum.TryParse<TerminationDocumentType>(Input.DocumentType, out var dt) ? dt : TerminationDocumentType.Other;

                await _terminationService.AddDocumentAsync(requestId, file.FileName, file.ContentType, ms.ToArray(), docType);
            }
        }
    }
}
