using HRMS.Infrastructure.Identity;
using HRMS.Application.Models;
using HRMS.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HRMS.UI.Pages.Resignation
{
    [Authorize(Roles = "Employee")]
    public class EditDraftModel : PageModel
    {
        private readonly IResignationService _resignationService;
        private readonly UserManager<ApplicationUser> _userManager;

        public EditDraftModel(IResignationService resignationService, UserManager<ApplicationUser> userManager)
        {
            _resignationService = resignationService;
            _userManager = userManager;
        }

        public ResignationRequestViewModel? Draft { get; set; }

        public async Task<IActionResult> OnGetAsync(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            Draft = await _resignationService.GetByIdAsync(id);

            if (Draft == null)
            {
                TempData["ErrorMessage"] = "Resignation request not found.";
                return RedirectToPage("/Transfer/Separation", new { ActiveTab = "Resignation" });
            }

            // Security: ensure this draft belongs to the current user
            if (!string.Equals(Draft.EmployeeEmail, user.Email, StringComparison.OrdinalIgnoreCase))
            {
                return Forbid();
            }

            // Only drafts can be edited
            if (Draft.Status != ResignationStatusEnum.Draft)
            {
                TempData["ErrorMessage"] = "Only Draft resignations can be edited.";
                return RedirectToPage("/Transfer/Separation", new { ActiveTab = "Resignation" });
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAsync(
            int id,
            string action,
            DateTime effectiveDate,
            string reasonForResignation,
            string? additionalRemarks,
            bool hasOutstandingLoans,
            bool isLoanGuarantor,
            List<IFormFile>? documents)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            // Verify ownership
            var existing = await _resignationService.GetByIdAsync(id);
            if (existing == null || !string.Equals(existing.EmployeeEmail, user.Email, StringComparison.OrdinalIgnoreCase))
                return Forbid();

            if (existing.Status != ResignationStatusEnum.Draft)
            {
                TempData["ErrorMessage"] = "Only Draft resignations can be edited.";
                return RedirectToPage("/Transfer/Separation", new { ActiveTab = "Resignation" });
            }

            // For submit: enforce strict validation
            if (action == "submit")
            {
                if (effectiveDate.Date < DateTime.Today.AddDays(14))
                {
                    TempData["ErrorMessage"] = "Effective date must be at least 14 days from today.";
                    return RedirectToPage("/Resignation/EditDraft", new { id });
                }

                if (string.IsNullOrWhiteSpace(reasonForResignation) || reasonForResignation.Length < 20)
                {
                    TempData["ErrorMessage"] = "Reason for resignation must be at least 20 characters.";
                    return RedirectToPage("/Resignation/EditDraft", new { id });
                }
            }
            else
            {
                // Draft: allow flexible data
                if (effectiveDate == default)
                    effectiveDate = existing.EffectiveDate;

                if (string.IsNullOrWhiteSpace(reasonForResignation))
                    reasonForResignation = existing.ReasonForResignation;
            }

            // Save updated fields
            var (updated, updateError) = await _resignationService.UpdateDraftAsync(
                id, effectiveDate, reasonForResignation, additionalRemarks, hasOutstandingLoans, isLoanGuarantor);

            if (!updated)
            {
                TempData["ErrorMessage"] = updateError;
                return RedirectToPage("/Resignation/EditDraft", new { id });
            }

            // Append any new documents
            if (documents != null)
            {
                var allowed = new[] { ".pdf", ".doc", ".docx", ".jpg", ".jpeg", ".png" };
                foreach (var file in documents)
                {
                    if (file.Length > 5 * 1024 * 1024) continue;
                    var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
                    if (!allowed.Contains(ext)) continue;
                    using var ms = new MemoryStream();
                    await file.CopyToAsync(ms);
                    await _resignationService.AddDocumentAsync(id, file.FileName, file.ContentType, ms.ToArray());
                }
            }

            if (action == "submit")
            {
                var (success, error) = await _resignationService.ValidateAndSubmitAsync(id);
                if (!success)
                {
                    TempData["ErrorMessage"] = error;
                    return RedirectToPage("/Resignation/EditDraft", new { id });
                }
                TempData["SuccessMessage"] = "Resignation submitted successfully. Your Branch Manager will review it shortly.";
                return RedirectToPage("/Transfer/Separation", new { ActiveTab = "Resignation" });
            }

            TempData["SuccessMessage"] = "Draft saved successfully.";
            return RedirectToPage("/Resignation/EditDraft", new { id });
        }
    }
}
