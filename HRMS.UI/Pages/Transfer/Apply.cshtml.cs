using HRMS.Infrastructure.Identity;
using HRMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using HRMS.Application.Services;
using HRMS.Domain.Entities.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;

namespace HRMS.UI.Pages.Transfer
{
    [Authorize(Roles = "HR Manager,Employee")]
    public class ApplyModel : PageModel
    {
        private readonly ITransferRequestService _transferService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _db;

        public ApplyModel(ITransferRequestService transferService, UserManager<ApplicationUser> userManager, ApplicationDbContext db)
        {
            _transferService = transferService;
            _userManager = userManager;
            _db = db;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public List<string> AvailableBranches { get; set; } = new();

        [BindProperty(SupportsGet = true)]
        public int? SelectedEmployeeId { get; set; }

        [BindProperty]
        public string? SearchQuery { get; set; }

        public List<Employee> SearchResults { get; set; } = new();

        public class InputModel
        {
            public string EmployeeName { get; set; } = string.Empty;
            public string EpfNumber { get; set; } = string.Empty;
            public string CurrentBranch { get; set; } = string.Empty;
            public string CurrentDesignation { get; set; } = string.Empty;
            public string Department { get; set; } = string.Empty;

            [Required(ErrorMessage = "Please select the branch you want to transfer to.")]
            [Display(Name = "Requested Branch")]
            public string RequestedBranch { get; set; } = string.Empty;

            [Required(ErrorMessage = "Preferred transfer date is required.")]
            [DataType(DataType.Date)]
            [Display(Name = "Preferred Transfer Date")]
            public DateTime? PreferredDate { get; set; }

            [Required(ErrorMessage = "Reason for transfer is required.")]
            [StringLength(500, MinimumLength = 20,
                ErrorMessage = "Reason must be between 20 and 500 characters.")]
            [Display(Name = "Reason for Transfer")]
            public string Reason { get; set; } = string.Empty;

            [Display(Name = "Supporting Document")]
            public IFormFile? Document { get; set; }
        }

        public async Task<IActionResult> OnGetAsync(int? employeeId)
        {
            if (employeeId.HasValue && (User.IsInRole("HR Manager") || User.IsInRole("Admin")))
                SelectedEmployeeId = employeeId;

            await PopulateUserDetailsAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostSearchAsync()
        {
            if (string.IsNullOrWhiteSpace(SearchQuery))
            {
                ModelState.AddModelError("SearchQuery", "Please enter a name or EPF number to search.");
                await PopulateUserDetailsAsync();
                return Page();
            }

            bool isNumeric = int.TryParse(SearchQuery, out int empId);

            SearchResults = await _db.Employees
                .Include(e => e.Branch)
                .Include(e => e.Designation)
                .Include(e => e.Department)
                .Where(e => e.FullName.Contains(SearchQuery) || (isNumeric && e.Id == empId))
                .Take(10)
                .ToListAsync();

            if (!SearchResults.Any())
                TempData["SearchError"] = "No employees found matching your search.";

            await PopulateUserDetailsAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            await PopulateUserDetailsAsync();

            if (Input.Document != null && Input.Document.Length > 5 * 1024 * 1024)
                ModelState.AddModelError("Input.Document", "File size must not exceed 5 MB.");

            if (Input.Document != null)
            {
                var allowedExtensions = new[] { ".pdf", ".doc", ".docx", ".jpg", ".jpeg", ".png" };
                var extension = Path.GetExtension(Input.Document.FileName).ToLowerInvariant();
                if (!allowedExtensions.Contains(extension))
                    ModelState.AddModelError("Input.Document", "Only PDF, DOC, DOCX, JPG, and PNG files are allowed.");
            }

            if (!ModelState.IsValid)
                return Page();

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var userRole = User.IsInRole("HR Manager") || User.IsInRole("Admin") ? "HR Manager" : "Employee";

            string targetEmail = user.Email!;
            DateTime joiningDate = user.DateOfJoining;

            if (SelectedEmployeeId.HasValue && (User.IsInRole("HR Manager") || User.IsInRole("Admin")))
            {
                var targetEmployee = await _db.Employees.FindAsync(SelectedEmployeeId.Value);
                if (targetEmployee != null)
                {
                    targetEmail = targetEmployee.Email;
                    joiningDate = targetEmployee.DateJoined ?? DateTime.Today;
                }
            }

            byte[]? documentData = null;
            string? documentFileName = null;
            string? documentContentType = null;

            if (Input.Document != null)
            {
                using var memoryStream = new MemoryStream();
                await Input.Document.CopyToAsync(memoryStream);
                documentData = memoryStream.ToArray();
                documentFileName = Input.Document.FileName;
                documentContentType = Input.Document.ContentType;
            }

            var (success, error, _) = await _transferService.ApplyTransferAsync(
                targetEmail, Input.EmployeeName, Input.EpfNumber,
                Input.CurrentBranch, Input.CurrentDesignation, Input.Department,
                Input.RequestedBranch, Input.Reason, Input.PreferredDate,
                user.Email!, userRole, joiningDate,
                documentData, documentFileName, documentContentType);

            if (!success)
            {
                if (error!.Contains("branch"))
                    ModelState.AddModelError("Input.RequestedBranch", error);
                else
                    ModelState.AddModelError("Input.PreferredDate", error);
                return Page();
            }

            TempData["SuccessMessage"] = "Transfer request submitted successfully!";
            return RedirectToPage("/Transfer/MyRequests");
        }

        private async Task PopulateUserDetailsAsync()
        {
            Employee? employee = null;
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return;

            if (SelectedEmployeeId.HasValue && (User.IsInRole("HR Manager") || User.IsInRole("Admin")))
            {
                employee = await _db.Employees
                    .Include(e => e.Branch)
                    .Include(e => e.Designation)
                    .Include(e => e.Department)
                    .FirstOrDefaultAsync(e => e.Id == SelectedEmployeeId.Value);
            }

            if (employee == null)
            {
                employee = await _db.Employees
                    .Include(e => e.Branch)
                    .Include(e => e.Designation)
                    .Include(e => e.Department)
                    .FirstOrDefaultAsync(e => e.Email == user.Email || (user.EmployeeId.HasValue && e.Id == user.EmployeeId));
            }

            if (employee != null)
            {
                Input.EmployeeName = employee.FullName;
                Input.EpfNumber = employee.EPFNumber;
                Input.CurrentBranch = employee.Branch?.Name ?? "N/A";
                Input.CurrentDesignation = employee.Designation?.Title ?? "N/A";
                Input.Department = employee.Department?.Name ?? "N/A";
            }
            else
            {
                Input.EmployeeName = user.FullName ?? "Unknown";
                Input.EpfNumber = user.EpfNumber ?? "N/A";
                Input.CurrentBranch = user.Branch ?? "N/A";
                Input.CurrentDesignation = user.Designation ?? "N/A";
                Input.Department = user.Department ?? "N/A";
            }

            var dbBranches = await _db.Branches.Select(b => b.Name).ToListAsync();
            AvailableBranches = dbBranches
                .Where(b => !string.Equals(b, Input.CurrentBranch, StringComparison.OrdinalIgnoreCase))
                .OrderBy(b => b)
                .ToList();
        }
    }
}
