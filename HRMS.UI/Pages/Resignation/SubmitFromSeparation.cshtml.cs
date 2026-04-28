using HRMS.Infrastructure.Identity;
using HRMS.UI.Models;
using HRMS.UI.Services;
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
            DateTime effectiveDate,
            string reasonForResignation,
            string? additionalRemarks,
            bool hasOutstandingLoans,
            bool isLoanGuarantor,
            List<IFormFile>? documents)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            // Basic date validation
            if (effectiveDate.Date < DateTime.Today.AddDays(14))
            {
                TempData["ErrorMessage"] = "Effective date must be at least 14 days from today.";
                return RedirectToPage("/Transfer/Separation", new { ActiveTab = "Resignation" });
            }

            if (string.IsNullOrWhiteSpace(reasonForResignation) || reasonForResignation.Length < 20)
            {
                TempData["ErrorMessage"] = "Reason for resignation must be at least 20 characters.";
                return RedirectToPage("/Transfer/Separation", new { ActiveTab = "Resignation" });
            }

            var noticeDays = (effectiveDate.Date - DateTime.Today).Days;

            var vm = new ResignationRequestViewModel
            {
                EmployeeName         = user.FullName,
                EpfNumber            = user.EpfNumber,
                EmployeeEmail        = user.Email!,
                Branch               = user.Branch,
                Department           = user.Department ?? "",
                Designation          = user.Designation,
                ReasonForResignation = reasonForResignation,
                ResignationDate      = DateTime.Today,
                EffectiveDate        = effectiveDate,
                NoticePeriodDays     = noticeDays,
                AdditionalRemarks    = additionalRemarks,
                HasOutstandingLoans  = hasOutstandingLoans,
                IsLoanGuarantor      = isLoanGuarantor,
                InitiatedBy          = user.Email!
            };

            var id = await _resignationService.CreateResignationRequestAsync(vm);

            // Upload docs
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
                    return RedirectToPage("/Transfer/Separation", new { ActiveTab = "Resignation" });
                }
                TempData["SuccessMessage"] = "Resignation submitted successfully. Your Branch Manager will review it shortly.";
            }
            else
            {
                TempData["SuccessMessage"] = "Resignation saved as draft.";
            }

            return RedirectToPage("/Transfer/Separation", new { ActiveTab = "Resignation" });
        }
    }
}
