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
    /// <summary>
    /// Handles form submission from the Resignation tab inside /Transfer/Separation.
    /// Redirects back to the Separation page after processing.
    /// </summary>
    [Authorize(Roles = "Employee")]
    public class SubmitFromSeparationModel : PageModel
    {
        private readonly IResignationService _resignationService;
        private readonly UserManager<ApplicationUser> _userManager;

        public SubmitFromSeparationModel(IResignationService resignationService, UserManager<ApplicationUser> userManager)
        {
            _resignationService = resignationService;
            _userManager = userManager;
        }

        public async Task<IActionResult> OnPostAsync(
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

            var finalEffectiveDate = effectiveDate.HasValue && effectiveDate.Value != default 
                ? effectiveDate.Value 
                : SriLankaTime.Today.AddMonths(1);

            // ── Strict validation applies to "submit" only, not drafts ──
            if (action == "submit")
            {
                if (finalEffectiveDate.Date < SriLankaTime.Today.AddMonths(1))
                {
                    TempData["ErrorMessage"] = "Last working day must be at least 1 month from the requesting date.";
                    return RedirectToPage("/Transfer/Separation", new { ActiveTab = "Resignation" });
                }

                if (string.IsNullOrWhiteSpace(reasonForResignation) || reasonForResignation.Trim().Length < 20)
                {
                    TempData["ErrorMessage"] = "Reason for resignation must be at least 20 characters.";
                    return RedirectToPage("/Transfer/Separation", new { ActiveTab = "Resignation" });
                }
            }

            var cleanReason = reasonForResignation?.Trim() ?? string.Empty;
            var noticeDays = Math.Max(0, (finalEffectiveDate.Date - SriLankaTime.Today).Days);

            var vm = new ResignationRequestViewModel
            {
                EmployeeName         = user.FullName,
                EpfNumber            = user.EpfNumber,
                EmployeeEmail        = user.Email!,
                Branch               = user.Branch,
                Department           = user.Department ?? "",
                Designation          = user.Designation,
                ReasonForResignation = cleanReason,
                ResignationDate      = SriLankaTime.Today,
                EffectiveDate        = finalEffectiveDate,
                NoticePeriodDays     = noticeDays,
                AdditionalRemarks    = additionalRemarks,
                HasOutstandingLoans  = hasOutstandingLoans,
                IsLoanGuarantor      = isLoanGuarantor,
                InitiatedBy          = user.Email!
            };

            // Always create as Draft first
            var id = await _resignationService.CreateResignationRequestAsync(vm);

            // Upload supporting documents (allowed for both draft and submit)
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
                // Validate documents are present, then move to SubmittedForApproval
                var (success, error) = await _resignationService.ValidateAndSubmitAsync(id);
                if (!success)
                {
                    TempData["ErrorMessage"] = error;
                    return RedirectToPage("/Transfer/Separation", new { ActiveTab = "Resignation" });
                }
                TempData["SuccessMessage"] = "Resignation submitted successfully. Your Branch Manager will review it shortly.";
            }
            else
            {
                TempData["SuccessMessage"] = "Resignation saved as draft. You can review and submit it later.";
            }

            return RedirectToPage("/Transfer/Separation", new { ActiveTab = "Resignation" });
        }

    }
}
