using HRMS.Application.Models;
using HRMS.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Claims;

namespace HRMS.UI.Pages.DeathProcess
{
    [Authorize(Roles = "Branch Manager,HR Manager")]
    public class ApplyModel : PageModel
    {
        private readonly IDeathService _deathService;

        public ApplyModel(IDeathService deathService)
        {
            _deathService = deathService;
        }

        [BindProperty]
        public DeathRequestViewModel RequestModel { get; set; } = new();

        public void OnGet(string? employeeName, string? epfNumber, string? email, string? branch, string? dept, string? designation)
        {
            // Auto fill these if coming from an employee directory list
            RequestModel.EmployeeName = employeeName ?? "";
            RequestModel.EpfNumber = epfNumber ?? "";
            RequestModel.EmployeeEmail = email ?? "";
            RequestModel.Branch = branch ?? "";
            RequestModel.Department = dept ?? "";
            RequestModel.Designation = designation ?? "";
            RequestModel.DateOfDeath = DateTime.Today;
        }

        public async Task<IActionResult> OnPostAsync(List<IFormFile> documents)
        {
            if (documents == null || documents.Count == 0)
            {
                ModelState.AddModelError("documents", "At least one mandatory document (e.g., Death Certificate) must be uploaded.");
            }

            if (!ModelState.IsValid)
            {
                return Page();
            }

            var initiatedByEmail = User.FindFirstValue(ClaimTypes.Email)!;
            
            try
            {
                var id = await _deathService.SubmitRequestAsync(RequestModel, documents!, initiatedByEmail);
                TempData["SuccessMessage"] = $"Death Request #{id} submitted successfully and forwarded to the Branch Manager review queue.";
                // Redirect to the appropriate review page based on their role
                if (User.IsInRole("HR Manager"))
                {
                    return RedirectToPage("/HRManager/ReviewDeathRequests");
                }
                return RedirectToPage("/BranchManager/ReviewDeathRequests");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "An error occurred: " + ex.Message);
                return Page();
            }
        }
    }
}
