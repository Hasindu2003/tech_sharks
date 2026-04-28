using HRMS.Infrastructure.Identity;
using HRMS.UI.Models;
using HRMS.UI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;

namespace HRMS.UI.Pages.Transfer
{
    [Authorize(Roles = "Employee,Branch Manager,HR Manager")]
    public class ApplyModel : PageModel
    {
        private readonly ITransferRequestService _transferService;
        private readonly UserManager<ApplicationUser> _userManager;

        public ApplyModel(ITransferRequestService transferService, UserManager<ApplicationUser> userManager)
        {
            _transferService = transferService;
            _userManager = userManager;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public List<string> AvailableBranches { get; set; } = new();

        private static readonly List<string> AllBranches =
        [
            "Head Office - Colombo",
            "Kandy Branch",
            "Galle Branch",
            "Negombo Branch",
            "Jaffna Branch",
            "Kurunegala Branch",
            "Matara Branch",
            "Ratnapura Branch",
            "Badulla Branch",
            "Anuradhapura Branch"
        ];

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

        public async Task<IActionResult> OnGetAsync()
        {
            await PopulateUserDetailsAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            await PopulateUserDetailsAsync();

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

            if (Input.RequestedBranch == Input.CurrentBranch)
            {
                ModelState.AddModelError("Input.RequestedBranch",
                    "You cannot request a transfer to your current branch.");
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
                return Page();

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var userRole = User.IsInRole("HR Manager") ? "HR Manager"
                         : User.IsInRole("Branch Manager") ? "Branch Manager"
                         : "Employee";
            var yearsOfService = (int)((DateTime.Today - user.DateOfJoining).TotalDays / 365.25);

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
                EmployeeName = user.FullName,
                EpfNumber = user.EpfNumber,
                EmployeeEmail = user.Email!,
                CurrentBranch = user.Branch,
                CurrentDesignation = user.Designation,
                Department = user.Department,
                RequestedBranch = Input.RequestedBranch,
                Reason = Input.Reason,
                PreferredDate = Input.PreferredDate!.Value,
                YearsOfService = yearsOfService,
                RequestedBy = user.Email!,
                RequestedByRole = userRole
            };

            await _transferService.CreateTransferRequestAsync(request, documentData, documentFileName, documentContentType);

            TempData["SuccessMessage"] = "Transfer request submitted successfully!";
            return RedirectToPage("/Transfer/MyRequests");
        }

        private async Task PopulateUserDetailsAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user != null)
            {
                Input.EmployeeName = user.FullName;
                Input.EpfNumber = user.EpfNumber;
                Input.CurrentBranch = user.Branch;
                Input.CurrentDesignation = user.Designation;
                Input.Department = user.Department;
                AvailableBranches = AllBranches.Where(b => b != user.Branch).ToList();
            }
        }
    }
}