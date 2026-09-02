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

namespace HRMS.UI.Pages.Resignation
{
    /// <summary>
    /// Handles form submission from the Resignation tab inside /Transfer/Separation.
    /// Redirects back to the Separation page after processing.
    /// </summary>
    [Authorize]
    public class SubmitFromSeparationModel : PageModel
    {
        private readonly IResignationService _resignationService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;

        public SubmitFromSeparationModel(
            IResignationService resignationService,
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext context)
        {
            _resignationService = resignationService;
            _userManager = userManager;
            _context = context;
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

            var emp = await _context.Employees
                .Include(e => e.Designation)
                .Include(e => e.Department)
                .Include(e => e.Branch)
                .FirstOrDefaultAsync(e => (user.EmployeeId.HasValue && e.Id == user.EmployeeId.Value) || (!string.IsNullOrEmpty(user.Email) && e.Email == user.Email));

            var userRole = User.IsInRole("HR Manager") ? "HR Manager"
                         : User.IsInRole("Area Manager") ? "Area Manager"
                         : User.IsInRole("Branch Manager") ? "Branch Manager"
                         : User.IsInRole("Department Head") ? "Department Head"
                         : User.IsInRole("Welfare Manager") ? "Welfare Manager"
                         : User.IsInRole("Admin") ? "Admin"
                         : "Employee";

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
                ReasonForResignation = cleanReason,
                ResignationDate      = SriLankaTime.Today,
                EffectiveDate        = finalEffectiveDate,
                NoticePeriodDays     = noticeDays,
                AdditionalRemarks    = additionalRemarks,
                HasOutstandingLoans  = hasOutstandingLoans,
                IsLoanGuarantor      = isLoanGuarantor,
                InitiatedBy          = user.Email ?? user.UserName ?? ""
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
                // Validate documents are present, then move to SubmittedForApproval or PendingHRReview
                var (success, error) = await _resignationService.ValidateAndSubmitAsync(id, userRole);
                if (!success)
                {
                    TempData["ErrorMessage"] = error;
                    return RedirectToPage("/Transfer/Separation", new { ActiveTab = "Resignation" });
                }

                bool isManager = await _resignationService.IsManagerialEmployeeAsync(vm.EmployeeEmail, vm.EpfNumber, vm.Designation, userRole, vm.Department);
                TempData["SuccessMessage"] = isManager
                    ? "Managerial resignation notice submitted directly for HR review."
                    : "Resignation submitted successfully. Your request is now pending review.";
            }
            else
            {
                TempData["SuccessMessage"] = "Resignation saved as draft. You can review and submit it later.";
            }

            return RedirectToPage("/Transfer/Separation", new { ActiveTab = "Resignation" });
        }
    }
}
