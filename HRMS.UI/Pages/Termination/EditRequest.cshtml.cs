using HRMS.Domain.Entities.Termination;
using HRMS.Infrastructure.Identity;
using HRMS.UI.Models;
using HRMS.UI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;

namespace HRMS.UI.Pages.Termination
{
    [Authorize(Roles = "HR Manager")]
    public class EditRequestModel : PageModel
    {
        private readonly ITerminationService _terminationService;
        private readonly UserManager<ApplicationUser> _userManager;

        public EditRequestModel(ITerminationService terminationService, UserManager<ApplicationUser> userManager)
        {
            _terminationService = terminationService;
            _userManager = userManager;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public TerminationRequestViewModel? CurrentRequest { get; set; }
        public List<TerminationDocumentViewModel> ExistingDocuments { get; set; } = new();

        public class InputModel
        {
            public int Id { get; set; }

            [Required(ErrorMessage = "Termination type is required.")]
            public TerminationTypeEnum TerminationType { get; set; }

            [Required(ErrorMessage = "Reason for termination is required.")]
            [StringLength(1000, MinimumLength = 10)]
            public string ReasonForTermination { get; set; } = string.Empty;

            [Required]
            [DataType(DataType.Date)]
            public DateTime? InitiationDate { get; set; }

            [Required]
            [DataType(DataType.Date)]
            public DateTime? EffectiveTerminationDate { get; set; }

            [StringLength(1000)]
            public string? SupervisorRemarks { get; set; }

            [StringLength(1000)]
            public string? SpecialRemarks { get; set; }

            [StringLength(2000)]
            public string? DirectObligations { get; set; }

            [StringLength(2000)]
            public string? IndirectObligations { get; set; }

            public bool HasOutstandingLoans { get; set; }
            public bool IsLoanGuarantor { get; set; }
            public bool HasOverridePermission { get; set; }

            public List<IFormFile>? Documents { get; set; }
            public string DocumentType { get; set; } = "Other";
        }

        public async Task<IActionResult> OnGetAsync(int id)
        {
            CurrentRequest = await _terminationService.GetTerminationByIdAsync(id);
            if (CurrentRequest == null) return NotFound();
            if (CurrentRequest.Status != TerminationStatusEnum.New && CurrentRequest.Status != TerminationStatusEnum.Rejected)
                return RedirectToPage("/Termination/Details", new { id });

            Input = new InputModel
            {
                Id = CurrentRequest.Id,
                TerminationType = CurrentRequest.TerminationType,
                ReasonForTermination = CurrentRequest.ReasonForTermination,
                InitiationDate = CurrentRequest.InitiationDate,
                EffectiveTerminationDate = CurrentRequest.EffectiveTerminationDate,
                SupervisorRemarks = CurrentRequest.SupervisorRemarks,
                SpecialRemarks = CurrentRequest.SpecialRemarks,
                DirectObligations = CurrentRequest.DirectObligations,
                IndirectObligations = CurrentRequest.IndirectObligations,
                HasOutstandingLoans = CurrentRequest.HasOutstandingLoans,
                IsLoanGuarantor = CurrentRequest.IsLoanGuarantor,
                HasOverridePermission = CurrentRequest.HasOverridePermission
            };

            ExistingDocuments = CurrentRequest.Documents;
            return Page();
        }

        public async Task<IActionResult> OnPostSaveAsync()
        {
            CurrentRequest = await _terminationService.GetTerminationByIdAsync(Input.Id);
            if (CurrentRequest == null) return NotFound();
            ExistingDocuments = CurrentRequest.Documents;

            ModelState.Remove("Input.ReasonForTermination");

            if (!ModelState.IsValid) return Page();

            var vm = new TerminationRequestViewModel
            {
                Id = Input.Id,
                TerminationType = Input.TerminationType,
                ReasonForTermination = Input.ReasonForTermination ?? "",
                InitiationDate = Input.InitiationDate ?? DateTime.Today,
                EffectiveTerminationDate = Input.EffectiveTerminationDate ?? DateTime.Today,
                SupervisorRemarks = Input.SupervisorRemarks,
                SpecialRemarks = Input.SpecialRemarks,
                DirectObligations = Input.DirectObligations,
                IndirectObligations = Input.IndirectObligations,
                HasOutstandingLoans = Input.HasOutstandingLoans,
                IsLoanGuarantor = Input.IsLoanGuarantor,
                HasOverridePermission = Input.HasOverridePermission
            };

            await _terminationService.UpdateTerminationRequestAsync(vm);
            await UploadDocumentsAsync(Input.Id);

            TempData["SuccessMessage"] = "Termination request updated successfully.";
            return RedirectToPage("/Termination/EditRequest", new { id = Input.Id });
        }

        public async Task<IActionResult> OnPostSubmitAsync()
        {
            CurrentRequest = await _terminationService.GetTerminationByIdAsync(Input.Id);
            if (CurrentRequest == null) return NotFound();
            ExistingDocuments = CurrentRequest.Documents;

            if (!ModelState.IsValid) return Page();

            var vm = new TerminationRequestViewModel
            {
                Id = Input.Id,
                TerminationType = Input.TerminationType,
                ReasonForTermination = Input.ReasonForTermination,
                InitiationDate = Input.InitiationDate ?? DateTime.Today,
                EffectiveTerminationDate = Input.EffectiveTerminationDate ?? DateTime.Today,
                SupervisorRemarks = Input.SupervisorRemarks,
                SpecialRemarks = Input.SpecialRemarks,
                DirectObligations = Input.DirectObligations,
                IndirectObligations = Input.IndirectObligations,
                HasOutstandingLoans = Input.HasOutstandingLoans,
                IsLoanGuarantor = Input.IsLoanGuarantor,
                HasOverridePermission = Input.HasOverridePermission
            };

            await _terminationService.UpdateTerminationRequestAsync(vm);
            await UploadDocumentsAsync(Input.Id);

            var (success, error) = await _terminationService.ValidateAndSubmitAsync(Input.Id);
            if (!success)
            {
                TempData["ErrorMessage"] = error;
                return RedirectToPage("/Termination/EditRequest", new { id = Input.Id });
            }

            TempData["SuccessMessage"] = "Termination request submitted for approval successfully.";
            return RedirectToPage("/Termination/Requests");
        }

        public async Task<IActionResult> OnPostRemoveDocumentAsync(int documentId, int requestId)
        {
            await _terminationService.RemoveDocumentAsync(documentId);
            return RedirectToPage("/Termination/EditRequest", new { id = requestId });
        }

        private async Task UploadDocumentsAsync(int requestId)
        {
            if (Input.Documents == null || !Input.Documents.Any()) return;

            var allowedExtensions = new[] { ".pdf", ".doc", ".docx", ".jpg", ".jpeg", ".png" };
            foreach (var file in Input.Documents)
            {
                if (file.Length > 5 * 1024 * 1024) continue;
                var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
                if (!allowedExtensions.Contains(ext)) continue;

                using var ms = new MemoryStream();
                await file.CopyToAsync(ms);

                var docType = Enum.TryParse<TerminationDocumentType>(Input.DocumentType, out var dt) ? dt : TerminationDocumentType.Other;
                await _terminationService.AddDocumentAsync(requestId, file.FileName, file.ContentType, ms.ToArray(), docType);
            }
        }
    }
}
