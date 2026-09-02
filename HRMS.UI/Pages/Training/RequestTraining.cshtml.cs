using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using HRMS.Infrastructure.Identity;
using HRMS.Infrastructure.Persistence;
using HRMS.Domain.Entities.Core;
using HRMS.Domain.Entities.Training;
using System;
using System.Linq;
using System.Threading.Tasks;
using EmployeeEntity = HRMS.Domain.Entities.Core.Employee;

using HRMS.Application.Services;

namespace HRMS.UI.Pages.Training
{
    [Authorize]
    public class RequestTrainingModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ITrainingNotificationService _trainingNotificationService;

        public RequestTrainingModel(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            ITrainingNotificationService trainingNotificationService)
        {
            _context = context;
            _userManager = userManager;
            _trainingNotificationService = trainingNotificationService;
        }

        [BindProperty]
        public TrainingProgramRequest TrainingRequest { get; set; } = new();

        [BindProperty]
        public string? CustomProgramTitle { get; set; }

        public string EmployeeName { get; set; } = "";
        public string EmployeeTypeDisplayName { get; set; } = "";
        public bool IsEligible { get; set; } = false;

        private bool CheckIsDutyAccount()
        {
            return User.IsInRole("Admin") ||
                   User.IsInRole("HR Manager") ||
                   User.IsInRole("HR Officer") ||
                   User.IsInRole("Branch Manager") ||
                   User.IsInRole("Area Manager") ||
                   User.IsInRole("Department Head");
        }

        private async Task<EmployeeEntity?> ResolveCurrentEmployeeAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            var query = _context.Employees
                .Include(e => e.Branch)
                .Include(e => e.Department)
                .Include(e => e.Designation)
                .Where(e => e.NIC != "DUTY-ACC" && !e.NIC.StartsWith("DUTY") && !e.NIC.StartsWith("DUTY-"));

            if (user != null)
            {
                if (user.EmployeeId.HasValue)
                {
                    var empById = await query.FirstOrDefaultAsync(e => e.Id == user.EmployeeId.Value);
                    if (empById != null) return empById;
                }

                foreach (var email in new[] { user.Email, user.UserName })
                {
                    if (string.IsNullOrWhiteSpace(email)) continue;
                    var empByEmail = await query.FirstOrDefaultAsync(e => e.Email == email);
                    if (empByEmail != null) return empByEmail;
                }
            }

            var identityName = User.Identity?.Name;
            if (!string.IsNullOrWhiteSpace(identityName))
            {
                var empByName = await query.FirstOrDefaultAsync(e => e.Email == identityName);
                if (empByName != null) return empByName;
            }

            return null;
        }

        public async Task<IActionResult> OnGetAsync()
        {
            if (CheckIsDutyAccount()) return Forbid();

            var employee = await ResolveCurrentEmployeeAsync();
            if (employee != null)
            {
                EmployeeName = employee.FullName ?? "Unknown";
                EmployeeTypeDisplayName = !string.IsNullOrWhiteSpace(employee.EmployeeType) 
                    ? employee.EmployeeType 
                    : (!string.IsNullOrWhiteSpace(employee.Status) ? employee.Status : "N/A");

                if (string.Equals(employee.EmployeeType, "Permanent", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(employee.Status, "Permanent", StringComparison.OrdinalIgnoreCase) ||
                    (employee.EmployeeType?.Contains("Permanent", StringComparison.OrdinalIgnoreCase) ?? false) ||
                    employee.DateConfirmed.HasValue)
                {
                    IsEligible = true;
                }
            }
            else
            {
                EmployeeName = User.Identity?.Name ?? "Employee";
                EmployeeTypeDisplayName = "N/A";
                IsEligible = false;
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (CheckIsDutyAccount()) return Forbid();

            var employee = await ResolveCurrentEmployeeAsync();
            if (employee == null)
            {
                ModelState.AddModelError("", "Duty accounts or unlinked profiles cannot submit training requests.");
                return Page();
            }

            bool eligible = string.Equals(employee.EmployeeType, "Permanent", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(employee.Status, "Permanent", StringComparison.OrdinalIgnoreCase) ||
                            (employee.EmployeeType?.Contains("Permanent", StringComparison.OrdinalIgnoreCase) ?? false) ||
                            employee.DateConfirmed.HasValue;

            if (!eligible)
            {
                ModelState.AddModelError("", "Training requests are restricted to permanent staff only.");
                await OnGetAsync();
                return Page();
            }

            var programTitle = TrainingRequest.Title;
            if (string.Equals(programTitle, "Other", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(programTitle, "Custom", StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(programTitle))
            {
                programTitle = CustomProgramTitle;
            }

            if (string.IsNullOrWhiteSpace(programTitle))
            {
                ModelState.AddModelError("TrainingRequest.Title", "Please select or type a training program name.");
                await OnGetAsync();
                return Page();
            }

            try 
            {
                var newRequest = new TrainingProgramRequest
                {
                    EmployeeId = employee.Id,
                    Title = programTitle.Trim(),
                    Description = TrainingRequest.Description ?? "",
                    RequestedDate = DateTime.Now,
                    Status = "Pending"
                };

                _context.TrainingProgramRequests.Add(newRequest);
                await _context.SaveChangesAsync();

                await _trainingNotificationService.NotifyTrainingRequestSubmittedAsync(newRequest.Id, newRequest.EmployeeId, newRequest.Title);

                TempData["SuccessMessage"] = "Your training request has been submitted successfully.";
                return RedirectToPage("./Dashboard");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Error: " + ex.Message);
                await OnGetAsync();
                return Page();
            }
        }
    }
}
