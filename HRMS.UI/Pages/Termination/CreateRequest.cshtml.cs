using HRMS.Domain.Entities.Termination;
using HRMS.Domain.Common;
using HRMS.Infrastructure.Identity;
using HRMS.Infrastructure.Persistence;
using HRMS.Application.Models;
using HRMS.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace HRMS.UI.Pages.Termination
{
    [Authorize(Roles = "HR Manager,HR Officer")]
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
            return RedirectToPage("/Separation/Dashboard", new { ActiveTab = "Terminations" });
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

            if (Input.Documents == null || !Input.Documents.Any() || Input.Documents.All(d => d == null || d.Length == 0))
            {
                ModelState.AddModelError("Input.Documents", "At least one supporting document must be attached to submit a termination request.");
                return Page();
            }

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
            return RedirectToPage("/Separation/Dashboard", new { ActiveTab = "Terminations" });
        }

        private async Task<ApplicationUser?> GetCurrentUserAsync()
        {
            var username = User.Identity?.Name;
            var user = await _userManager.GetUserAsync(User);
            if (user == null && !string.IsNullOrEmpty(username))
            {
                user = await _userManager.FindByNameAsync(username) ?? await _userManager.FindByEmailAsync(username);
            }
            return user;
        }

        private async Task<(HashSet<int> DutyEmployeeIds, HashSet<string> DutyIdentifiers)> GetDutyAccountExclusionsAsync()
        {
            var dutyEmployeeIds = new HashSet<int>();
            var dutyIdentifiers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var dutyRoles = new[] { "Admin", "HR Manager", "HR Officer", "Branch Manager", "Area Manager", "Department Head" };
            foreach (var role in dutyRoles)
            {
                var usersInRole = await _userManager.GetUsersInRoleAsync(role);
                foreach (var u in usersInRole)
                {
                    if (u.EmployeeId.HasValue && u.EmployeeId.Value > 0)
                        dutyEmployeeIds.Add(u.EmployeeId.Value);

                    if (!string.IsNullOrWhiteSpace(u.Email))
                        dutyIdentifiers.Add(u.Email.Trim());

                    if (!string.IsNullOrWhiteSpace(u.UserName))
                        dutyIdentifiers.Add(u.UserName.Trim());

                    if (!string.IsNullOrWhiteSpace(u.EpfNumber))
                        dutyIdentifiers.Add(u.EpfNumber.Trim());
                }
            }

            return (dutyEmployeeIds, dutyIdentifiers);
        }

        private async Task PopulateEmployeesAsync()
        {
            var hrUser = await GetCurrentUserAsync();
            var (dutyEmployeeIds, dutyIdentifiers) = await GetDutyAccountExclusionsAsync();

            var isHROfficer = User.IsInRole("HR Officer") && !User.IsInRole("HR Manager");
            var assignedBranchIds = new HashSet<int>();
            var assignedBranchNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (isHROfficer && hrUser != null)
            {
                if (!string.IsNullOrWhiteSpace(hrUser.ManagedBranches))
                {
                    var ids = hrUser.ManagedBranches
                        .Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(s => int.TryParse(s.Trim(), out int id) ? id : 0)
                        .Where(id => id > 0);
                    foreach (var id in ids) assignedBranchIds.Add(id);
                }

                if (!string.IsNullOrWhiteSpace(hrUser.Branch) && hrUser.Branch != "Multiple")
                {
                    assignedBranchNames.Add(hrUser.Branch.Trim());
                    var b = await _context.Branches.FirstOrDefaultAsync(br => br.Name == hrUser.Branch.Trim());
                    if (b != null) assignedBranchIds.Add(b.Id);
                }

                if (assignedBranchIds.Any())
                {
                    var names = await _context.Branches
                        .Where(br => assignedBranchIds.Contains(br.Id))
                        .Select(br => br.Name)
                        .ToListAsync();
                    foreach (var n in names) assignedBranchNames.Add(n.Trim());
                }
            }

            var options = new Dictionary<string, EmployeeOption>(StringComparer.OrdinalIgnoreCase);

            var query = _context.Employees
                .Include(e => e.Branch)
                .Include(e => e.Department)
                .Include(e => e.Designation)
                .Where(e => !e.NIC.StartsWith("DUTY")
                         && e.NIC != "DUTY-ACC" 
                         && e.Status != "Draft" 
                         && e.Status != "Terminated" 
                         && e.Status != "Resigned");

            if (isHROfficer && assignedBranchIds.Any())
            {
                query = query.Where(e => assignedBranchIds.Contains(e.BranchId));
            }
            else if (isHROfficer && assignedBranchNames.Any())
            {
                query = query.Where(e => e.Branch != null && assignedBranchNames.Contains(e.Branch.Name));
            }

            var dbEmployees = await query.ToListAsync();

            var filteredDbEmployees = dbEmployees
                .Where(e => !dutyEmployeeIds.Contains(e.Id)
                         && (string.IsNullOrEmpty(e.Email) || !dutyIdentifiers.Contains(e.Email.Trim()))
                         && (string.IsNullOrEmpty(e.EPFNumber) || !dutyIdentifiers.Contains(e.EPFNumber.Trim())))
                .ToList();

            foreach (var e in filteredDbEmployees)
            {
                var email = !string.IsNullOrWhiteSpace(e.Email) ? e.Email.Trim() : $"{e.EPFNumber}@kanrich.lk";
                options[email] = new EmployeeOption
                {
                    Email = email,
                    FullName = e.FullName,
                    EpfNumber = !string.IsNullOrWhiteSpace(e.EPFNumber) ? e.EPFNumber : "N/A",
                    Branch = e.Branch != null ? e.Branch.Name : "N/A",
                    Department = e.Department != null ? e.Department.Name : "General",
                    Designation = e.Designation != null ? e.Designation.Title : "Staff"
                };
            }

            var employeeUsers = await _userManager.GetUsersInRoleAsync("Employee");
            foreach (var u in employeeUsers)
            {
                if (string.IsNullOrWhiteSpace(u.Email)) continue;

                // Exclude if user belongs to any duty account identifier or duty employee id
                if (dutyIdentifiers.Contains(u.Email.Trim()) ||
                    (!string.IsNullOrWhiteSpace(u.UserName) && dutyIdentifiers.Contains(u.UserName.Trim())) ||
                    (!string.IsNullOrWhiteSpace(u.EpfNumber) && dutyIdentifiers.Contains(u.EpfNumber.Trim())))
                    continue;

                if (u.EmployeeId.HasValue && dutyEmployeeIds.Contains(u.EmployeeId.Value))
                    continue;

                // If HR Officer, scope to assigned branches
                if (isHROfficer && (assignedBranchNames.Any() || assignedBranchIds.Any()))
                {
                    if (string.IsNullOrWhiteSpace(u.Branch) || !assignedBranchNames.Contains(u.Branch.Trim()))
                        continue;
                }

                if (!options.ContainsKey(u.Email))
                {
                    options[u.Email] = new EmployeeOption
                    {
                        Email = u.Email,
                        FullName = u.FullName,
                        EpfNumber = u.EpfNumber,
                        Branch = u.Branch,
                        Department = u.Department ?? "General",
                        Designation = u.Designation
                    };
                }
            }

            Employees = options.Values.OrderBy(e => e.FullName).ToList();
        }

        private async Task<EmployeeOption?> GetSelectedEmployeeAsync()
        {
            await PopulateEmployeesAsync();
            return Employees.FirstOrDefault(e => e.Email.Equals(Input.SelectedEmployeeEmail, StringComparison.OrdinalIgnoreCase));
        }

        private bool ValidateDates()
        {
            if (Input.EffectiveTerminationDate.HasValue)
            {
                if (Input.EffectiveTerminationDate.Value.Date < SriLankaTime.Today)
                {
                    ModelState.AddModelError("Input.EffectiveTerminationDate", "Effective termination date cannot be before today.");
                    return false;
                }
            }
            return true;
        }

        private TerminationRequestViewModel BuildViewModel(EmployeeOption emp, ApplicationUser user)
        {
            return new TerminationRequestViewModel
            {
                EmployeeName = emp.FullName,
                EpfNumber = emp.EpfNumber,
                EmployeeEmail = emp.Email,
                Branch = emp.Branch,
                Department = emp.Department,
                Designation = emp.Designation,
                TerminationType = Input.TerminationType,
                ReasonForTermination = Input.ReasonForTermination ?? "",
                InitiationDate = SriLankaTime.Today,
                EffectiveTerminationDate = Input.EffectiveTerminationDate ?? SriLankaTime.Today,
                SupervisorRemarks = Input.SupervisorRemarks,
                SpecialRemarks = Input.SpecialRemarks,
                DirectObligations = Input.DirectObligations,
                IndirectObligations = Input.IndirectObligations,
                HasOutstandingLoans = Input.HasOutstandingLoans,
                IsLoanGuarantor = Input.IsLoanGuarantor,
                HasOverridePermission = Input.HasOverridePermission,
                InitiatedBy = user.Email ?? user.UserName ?? "HR Officer",
                InitiatedByRole = "HR Officer"
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
