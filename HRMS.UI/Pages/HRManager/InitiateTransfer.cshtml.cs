using HRMS.Infrastructure.Identity;
using HRMS.Infrastructure.Persistence;
using HRMS.Application.Models;
using HRMS.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace HRMS.UI.Pages.HRManager
{
    [Authorize(Roles = "HR Manager")]
    public class InitiateTransferModel : PageModel
    {
        private readonly ITransferRequestService _transferService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;

        public InitiateTransferModel(
            ITransferRequestService transferService,
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext context)
        {
            _transferService = transferService;
            _userManager = userManager;
            _context = context;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public List<SelectListItem> Employees { get; set; } = new();
        public List<string> AllBranches { get; set; } = new();


        public class InputModel
        {
            [Required(ErrorMessage = "Please select an employee.")]
            [Range(1, int.MaxValue, ErrorMessage = "Please select an employee.")]
            [Display(Name = "Employee")]
            public int? SelectedEmployeeId { get; set; }

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

        public async Task<IActionResult> OnGetAsync()
        {
            var hrUser = await _userManager.GetUserAsync(User);
            if (hrUser == null) return Challenge();
            await PopulateDropdownsAsync(hrUser.Branch);
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var hrUser = await _userManager.GetUserAsync(User);
            if (hrUser == null) return Challenge();

            if (Input.PreferredDate.HasValue)
            {
                var minDate = DateTime.Today.AddDays(7);
                var maxDate = DateTime.Today.AddYears(1);

                if (Input.PreferredDate.Value.Date < minDate)
                {
                    ModelState.AddModelError("Input.PreferredDate",
                        "Preferred date must be at least 7 days from today.");
                }
                else if (Input.PreferredDate.Value.Date > maxDate)
                {
                    ModelState.AddModelError("Input.PreferredDate",
                        "Preferred date cannot be more than 1 year from today.");
                }
            }

            if (Input.Document != null && Input.Document.Length > 5 * 1024 * 1024)
            {
                ModelState.AddModelError("Input.Document", "File size must not exceed 5 MB.");
            }

            if (Input.Document != null)
            {
                var allowedExtensions = new[] { ".pdf", ".doc", ".docx", ".jpg", ".jpeg", ".png" };
                var extension = Path.GetExtension(Input.Document.FileName).ToLowerInvariant();
                if (!allowedExtensions.Contains(extension))
                {
                    ModelState.AddModelError("Input.Document",
                        "Only PDF, DOC, DOCX, JPG, and PNG files are allowed.");
                }
            }

            if (!ModelState.IsValid)
            {
                await PopulateDropdownsAsync(hrUser.Branch);
                return Page();
            }

            var targetEmployee = await _context.Employees
                .Where(e => e.Id == Input.SelectedEmployeeId)
                .Select(e => new
                {
                    e.Id,
                    e.FullName,
                    e.EPFNumber,
                    e.Email,
                    e.DateJoined,
                    BranchName = e.Branch.Name,
                    DesignationTitle = e.Designation != null ? e.Designation.Title : "",
                    DepartmentName = e.Department != null ? e.Department.Name : ""
                })
                .FirstOrDefaultAsync();

            if (targetEmployee == null)
            {
                ModelState.AddModelError("Input.SelectedEmployeeId", "Invalid employee selected.");
                await PopulateDropdownsAsync(hrUser.Branch);
                return Page();
            }

            // Enforce branch restriction: HR Manager can only initiate transfers for employees in their own branch
            if (targetEmployee.BranchName != hrUser.Branch)
            {
                ModelState.AddModelError("Input.SelectedEmployeeId",
                    "You can only initiate transfers for employees in your branch.");
                await PopulateDropdownsAsync(hrUser.Branch);
                return Page();
            }

            if (Input.RequestedBranch == targetEmployee.BranchName)
            {
                ModelState.AddModelError("Input.RequestedBranch",
                    $"The employee is already at {targetEmployee.BranchName}. You cannot request a transfer to their current branch.");
                await PopulateDropdownsAsync(hrUser.Branch);
                return Page();
            }

            var joiningDate = targetEmployee.DateJoined ?? DateTime.Today;
            var yearsOfService = (int)((DateTime.Today - joiningDate).TotalDays / 365.25);

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

            var request = new TransferRequestViewModel
            {
                EmployeeName = targetEmployee.FullName,
                EpfNumber = targetEmployee.EPFNumber,
                EmployeeEmail = targetEmployee.Email,
                CurrentBranch = targetEmployee.BranchName,
                CurrentDesignation = targetEmployee.DesignationTitle,
                Department = targetEmployee.DepartmentName,
                RequestedBranch = Input.RequestedBranch,
                Reason = Input.Reason,
                PreferredDate = Input.PreferredDate!.Value,
                YearsOfService = yearsOfService,
                JoinDate = targetEmployee.DateJoined,
                RequestedBy = hrUser.Email!,
                RequestedByRole = "HR Manager"
            };

            await _transferService.CreateTransferRequestAsync(request, documentData, documentFileName, documentContentType);

            TempData["SuccessMessage"] = $"Transfer request for {targetEmployee.FullName} initiated successfully!";
            return RedirectToPage("/HRManager/ReviewTransfers");
        }

        private async Task PopulateDropdownsAsync(string hrBranch)
        {
            // Collect emails of all duty accounts so they can be excluded
            var dutyEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var role in new[] { "HR Manager", "Branch Manager", "Area Manager", "Department Head" })
            {
                var roleUsers = await _userManager.GetUsersInRoleAsync(role);
                foreach (var u in roleUsers)
                    if (u.Email != null) dutyEmails.Add(u.Email);
            }

            var employees = await _context.Employees
                .Where(e => e.Branch.Name == hrBranch && !dutyEmails.Contains(e.Email))
                .OrderBy(e => e.FullName)
                .Select(e => new
                {
                    e.Id,
                    e.FullName,
                    e.EPFNumber,
                    DesignationTitle = e.Designation != null ? e.Designation.Title : "N/A"
                })
                .ToListAsync();

            Employees = employees.Select(e => new SelectListItem
            {
                Value = e.Id.ToString(),
                Text = $"{e.FullName} ({e.EPFNumber}) - {e.DesignationTitle}"
            }).ToList();

            AllBranches = await _context.Branches.Select(b => b.Name).OrderBy(b => b).ToListAsync();
        }
    }
}
