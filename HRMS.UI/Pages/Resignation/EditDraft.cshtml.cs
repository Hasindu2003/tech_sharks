using HRMS.Infrastructure.Identity;
using HRMS.Application.Models;
using HRMS.Application.Services;
using HRMS.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HRMS.UI.Pages.Resignation
{
    [Authorize]
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
            DateTime? effectiveDate,
            string? reasonForResignation,
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

            var finalEffectiveDate = effectiveDate.HasValue && effectiveDate.Value != default 
                ? effectiveDate.Value 
                : (existing.EffectiveDate != default ? existing.EffectiveDate : SriLankaTime.Today.AddMonths(1));

            // For submit: enforce strict validation
            if (action == "submit")
            {
                if (finalEffectiveDate.Date < SriLankaTime.Today.AddMonths(1))
                {
                    TempData["ErrorMessage"] = "Last working day must be at least 1 month from the requesting date.";
                    return RedirectToPage("/Resignation/EditDraft", new { id });
                }

                if (string.IsNullOrWhiteSpace(reasonForResignation) || reasonForResignation.Trim().Length < 20)
                {
                    TempData["ErrorMessage"] = "Reason for resignation must be at least 20 characters.";
                    return RedirectToPage("/Resignation/EditDraft", new { id });
                }
            }

            var cleanReason = reasonForResignation?.Trim() ?? existing.ReasonForResignation ?? string.Empty;

            // Save updated fields
            (bool updated, string? updateError) = await _resignationService.UpdateDraftAsync(
                id, finalEffectiveDate, cleanReason, additionalRemarks, hasOutstandingLoans, isLoanGuarantor);

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
                    return RedirectToPage("/Resignation/EditDraft", new { id });
                }

                bool isManager = await _resignationService.IsManagerialEmployeeAsync(existing.EmployeeEmail, existing.EpfNumber, existing.Designation, userRole, existing.Department);
                TempData["SuccessMessage"] = isManager
                    ? "Managerial resignation notice submitted directly for HR review."
                    : "Resignation submitted successfully. Your request is now pending review.";
                return RedirectToPage("/Transfer/Separation", new { ActiveTab = "Resignation" });
            }

            TempData["SuccessMessage"] = "Draft saved successfully.";
            return RedirectToPage("/Resignation/EditDraft", new { id });
        }

        public async Task<IActionResult> OnPostDeleteAsync(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var identifier = user.Email ?? user.UserName ?? "";
            (bool success, string? error) = await _resignationService.DeleteDraftAsync(id, identifier);
            if (!success)
            {
                TempData["ErrorMessage"] = error;
                return RedirectToPage("/Resignation/EditDraft", new { id });
            }

            TempData["SuccessMessage"] = "Draft resignation has been deleted successfully.";
            return RedirectToPage("/Transfer/Separation", new { ActiveTab = "Resignation" });
        }
    }
}
