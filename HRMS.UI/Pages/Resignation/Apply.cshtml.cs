using HRMS.Infrastructure.Identity;
using HRMS.Application.Models;
using HRMS.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;

namespace HRMS.UI.Pages.Resignation
{
    [Authorize(Roles = "Admin,HR Manager,Area Manager,Branch Manager,Employee")]
    public class ApplyModel : PageModel
    {
        private readonly IResignationService _resignationService;
        private readonly UserManager<ApplicationUser> _userManager;

        public ApplyModel(IResignationService resignationService, UserManager<ApplicationUser> userManager)
        {
            _resignationService = resignationService;
            _userManager = userManager;
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

            var (_, _, id) = await _resignationService.CreateResignationAsync(
                CurrentUser.Email!, CurrentUser.FullName, CurrentUser.EpfNumber,
                CurrentUser.Branch, CurrentUser.Department, CurrentUser.Designation,
                Input.ReasonForResignation ?? "", Input.EffectiveDate, Input.AdditionalRemarks,
                Input.HasOutstandingLoans, Input.IsLoanGuarantor, Input.HasOverridePermission,
                Input.ObligationDetails, submitNow: false);

            await UploadDocumentsAsync(id);

            TempData["SuccessMessage"] = "Resignation request saved as draft.";
            return RedirectToPage("/Resignation/MyRequests");
        }

        public async Task<IActionResult> OnPostSubmitAsync()
        {
            CurrentUser = await _userManager.GetUserAsync(User);
            if (CurrentUser == null) return Challenge();

            if (!ModelState.IsValid) return Page();

            var (success, error, id) = await _resignationService.CreateResignationAsync(
                CurrentUser.Email!, CurrentUser.FullName, CurrentUser.EpfNumber,
                CurrentUser.Branch, CurrentUser.Department, CurrentUser.Designation,
                Input.ReasonForResignation, Input.EffectiveDate, Input.AdditionalRemarks,
                Input.HasOutstandingLoans, Input.IsLoanGuarantor, Input.HasOverridePermission,
                Input.ObligationDetails, submitNow: true);

            if (!success)
            {
                ModelState.AddModelError("Input.EffectiveDate", error!);
                return Page();
            }

            await UploadDocumentsAsync(id);

            TempData["SuccessMessage"] = "Resignation request submitted successfully. Your Branch Manager will review it shortly.";
            return RedirectToPage("/Resignation/MyRequests");
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
