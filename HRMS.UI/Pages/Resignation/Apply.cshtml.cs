using HRMS.Infrastructure.Identity;
using HRMS.Infrastructure.Persistence;
using HRMS.Application.Models;
using HRMS.Application.Services;
using HRMS.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace HRMS.UI.Pages.Resignation
{
    [Authorize]
    public class ApplyModel : PageModel
    {
        private readonly IResignationService _resignationService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;

        public ApplyModel(
            IResignationService resignationService,
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext context)
        {
            _resignationService = resignationService;
            _userManager = userManager;
            _context = context;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public ApplicationUser? CurrentUser { get; set; }

        public class InputModel
        {
            [Required(ErrorMessage = "Reason for resignation is required.")]
            [StringLength(1000, MinimumLength = 20, ErrorMessage = "Reason must be between 20 and 1000 characters.")]
            [Display(Name = "Reason for Resignation")]
            public string ReasonForResignation { get; set; } = string.Empty;

            [Required(ErrorMessage = "Effective (last working) date is required.")]
            [DataType(DataType.Date)]
            [Display(Name = "Effective Date (Last Working Day)")]
            public DateTime? EffectiveDate { get; set; }

            [StringLength(1000)]
            [Display(Name = "Additional Remarks")]
            public string? AdditionalRemarks { get; set; }

            [Display(Name = "I have outstanding loan balances")]
            public bool HasOutstandingLoans { get; set; }

            [Display(Name = "I am a guarantor for another employee's loan")]
            public bool IsLoanGuarantor { get; set; }

            [Display(Name = "Senior management override granted")]
            public bool HasOverridePermission { get; set; }

            [StringLength(2000)]
            [Display(Name = "Obligation Details")]
            public string? ObligationDetails { get; set; }

            [Display(Name = "Supporting Documents")]
            public List<IFormFile>? Documents { get; set; }
        }

        public async Task<IActionResult> OnGetAsync()
        {
            CurrentUser = await _userManager.GetUserAsync(User);
            if (CurrentUser == null) return Challenge();
            return Page();
        }

        public async Task<IActionResult> OnPostSaveDraftAsync()
        {
            CurrentUser = await _userManager.GetUserAsync(User);
            if (CurrentUser == null) return Challenge();

            ModelState.Remove("Input.ReasonForResignation");
            ModelState.Remove("Input.EffectiveDate");

            if (!ModelState.IsValid) return Page();

            var id = await SaveRequestAsync(CurrentUser, false);
            await UploadDocumentsAsync(id);

            TempData["SuccessMessage"] = "Resignation request saved as draft.";
            return RedirectToPage("/Resignation/MyRequests");
        }

        public async Task<IActionResult> OnPostSubmitAsync()
        {
            CurrentUser = await _userManager.GetUserAsync(User);
            if (CurrentUser == null) return Challenge();

            if (Input.EffectiveDate.HasValue)
            {
                var minDate = SriLankaTime.Today.AddMonths(1);
                if (Input.EffectiveDate.Value.Date < minDate)
                    ModelState.AddModelError("Input.EffectiveDate", "Last working day must be at least 1 month from the requesting date.");
            }

            if (!ModelState.IsValid) return Page();

            var id = await SaveRequestAsync(CurrentUser, true);
            await UploadDocumentsAsync(id);

            var userRole = User.IsInRole("HR Manager") ? "HR Manager"
                         : User.IsInRole("Area Manager") ? "Area Manager"
                         : User.IsInRole("Branch Manager") ? "Branch Manager"
                         : User.IsInRole("Department Head") ? "Department Head"
                         : User.IsInRole("Welfare Manager") ? "Welfare Manager"
                         : User.IsInRole("Admin") ? "Admin"
                         : "Employee";

            var (success, error) = await _resignationService.ValidateAndSubmitAsync(id, userRole);
            if (!success)
            {
                TempData["ErrorMessage"] = error;
                return RedirectToPage("/Resignation/MyRequests");
            }

            bool isManager = await _resignationService.IsManagerialEmployeeAsync(CurrentUser.Email, CurrentUser.EpfNumber, CurrentUser.Designation, userRole, CurrentUser.Department);
            TempData["SuccessMessage"] = isManager
                ? "Managerial resignation notice submitted directly for HR review."
                : "Resignation request submitted successfully. Your request is now pending review.";
            return RedirectToPage("/Resignation/MyRequests");
        }

        private async Task<int> SaveRequestAsync(ApplicationUser user, bool submit)
        {
            var today = SriLankaTime.Today;
            var effectiveDate = Input.EffectiveDate ?? today.AddMonths(1);
            var noticeDays = (effectiveDate - today).Days;

            var emp = await _context.Employees
                .Include(e => e.Designation)
                .Include(e => e.Department)
                .Include(e => e.Branch)
                .FirstOrDefaultAsync(e => (user.EmployeeId.HasValue && e.Id == user.EmployeeId.Value) || (!string.IsNullOrEmpty(user.Email) && e.Email == user.Email));

            var designation = !string.IsNullOrWhiteSpace(user.Designation) ? user.Designation : emp?.Designation?.Title ?? "";
            var department = !string.IsNullOrWhiteSpace(user.Department) ? user.Department : emp?.Department?.Name ?? "";
            var branch = !string.IsNullOrWhiteSpace(user.Branch) ? user.Branch : emp?.Branch?.Name ?? "";

            var vm = new ResignationRequestViewModel
            {
                EmployeeName         = !string.IsNullOrWhiteSpace(user.FullName) ? user.FullName : emp?.FullName ?? "",
                EpfNumber            = !string.IsNullOrWhiteSpace(user.EpfNumber) ? user.EpfNumber : emp?.EPFNumber ?? "",
                EmployeeEmail        = user.Email ?? emp?.Email ?? "",
                Branch               = branch,
                Department           = department,
                Designation          = designation,
                ReasonForResignation = Input.ReasonForResignation ?? "",
                ResignationDate      = today,
                EffectiveDate        = effectiveDate,
                NoticePeriodDays     = noticeDays,
                AdditionalRemarks    = Input.AdditionalRemarks,
                HasOutstandingLoans  = Input.HasOutstandingLoans,
                IsLoanGuarantor      = Input.IsLoanGuarantor,
                HasOverridePermission = Input.HasOverridePermission,
                ObligationDetails    = Input.ObligationDetails,
                InitiatedBy          = user.Email ?? user.UserName ?? ""
            };

            return await _resignationService.CreateResignationRequestAsync(vm);
        }

        private async Task UploadDocumentsAsync(int requestId)
        {
            if (Input.Documents == null || !Input.Documents.Any()) return;

            var allowed = new[] { ".pdf", ".doc", ".docx", ".jpg", ".jpeg", ".png" };
            foreach (var file in Input.Documents)
            {
                if (file.Length > 5 * 1024 * 1024) continue;
                var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
                if (!allowed.Contains(ext)) continue;

                using var ms = new MemoryStream();
                await file.CopyToAsync(ms);
                await _resignationService.AddDocumentAsync(requestId, file.FileName, file.ContentType, ms.ToArray());
            }
        }
    }
}
