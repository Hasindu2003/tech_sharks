using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using HRMS.Infrastructure.Identity;
using HRMS.Infrastructure.Persistence;
using HRMS.Domain.Entities.Core;
using HRMS.Domain.Entities.Training;
using HRMS.Application.Services;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System;
using EmployeeEntity = HRMS.Domain.Entities.Core.Employee;

namespace HRMS.UI.Pages.Training
{
    [Authorize]
    public class SessionsModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ITrainingNotificationService _trainingNotificationService;

        public SessionsModel(
            ApplicationDbContext context, 
            UserManager<ApplicationUser> userManager,
            ITrainingNotificationService trainingNotificationService)
        {
            _context = context;
            _userManager = userManager;
            _trainingNotificationService = trainingNotificationService;
        }

        public List<ScheduledSessionDto> UpcomingSessions { get; set; } = new();
        public List<ScheduledSessionDto> PastSessions { get; set; } = new();
        public List<EmployeeTrainingRequestDto> MyTrainingRequests { get; set; } = new();

        [BindProperty]
        public TrainingProgramRequest TrainingRequest { get; set; } = new();

        [BindProperty]
        public string? CustomProgramTitle { get; set; }

        public string EmployeeName { get; set; } = "";
        public string EmployeeTypeDisplayName { get; set; } = "";
        public bool IsEligible { get; set; } = false;
        public bool CanRequestTraining { get; set; } = false;
        public bool IsDutyAccount { get; set; } = false;

        private bool CheckIsDutyAccount()
        {
            return User.IsInRole("Admin") ||
                   User.IsInRole("HR Manager") ||
                   User.IsInRole("HR Officer") ||
                   User.IsInRole("Branch Manager") ||
                   User.IsInRole("Area Manager") ||
                   User.IsInRole("Department Head");
        }

        private async Task<List<int>> GetAllowedBranchIdsAsync()
        {
            if (User.IsInRole("HR Manager"))
            {
                return await _context.Branches.Select(b => b.Id).ToListAsync();
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null && !string.IsNullOrEmpty(User.Identity?.Name))
            {
                user = await _userManager.FindByNameAsync(User.Identity.Name) ?? await _userManager.FindByEmailAsync(User.Identity.Name);
            }

            var allowedBranchIds = new List<int>();
            if (!string.IsNullOrWhiteSpace(user?.ManagedBranches))
            {
                var rawTokens = user.ManagedBranches.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                foreach (var token in rawTokens)
                {
                    if (int.TryParse(token, out int bid))
                    {
                        allowedBranchIds.Add(bid);
                    }
                    else
                    {
                        var bMatch = await _context.Branches.FirstOrDefaultAsync(b => b.Name == token);
                        if (bMatch != null) allowedBranchIds.Add(bMatch.Id);
                    }
                }
            }

            if (!allowedBranchIds.Any() && !string.IsNullOrWhiteSpace(user?.Branch))
            {
                var bMatch = await _context.Branches.FirstOrDefaultAsync(b => b.Name == user.Branch);
                if (bMatch != null) allowedBranchIds.Add(bMatch.Id);
            }

            if (!allowedBranchIds.Any() && user?.EmployeeId.HasValue == true)
            {
                var emp = await _context.Employees.FirstOrDefaultAsync(e => e.Id == user.EmployeeId.Value);
                if (emp != null) allowedBranchIds.Add(emp.BranchId);
            }

            return allowedBranchIds.Distinct().ToList();
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
            if (User.IsInRole("Admin"))
            {
                return Forbid();
            }

            IsDutyAccount = CheckIsDutyAccount();

            var currentEmp = await ResolveCurrentEmployeeAsync();
            if (currentEmp != null)
            {
                EmployeeName = currentEmp.FullName ?? "Unknown";
                EmployeeTypeDisplayName = !string.IsNullOrWhiteSpace(currentEmp.EmployeeType)
                    ? currentEmp.EmployeeType
                    : (!string.IsNullOrWhiteSpace(currentEmp.Status) ? currentEmp.Status : "N/A");

                if (string.Equals(currentEmp.EmployeeType, "Permanent", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(currentEmp.Status, "Permanent", StringComparison.OrdinalIgnoreCase) ||
                    (currentEmp.EmployeeType?.Contains("Permanent", StringComparison.OrdinalIgnoreCase) ?? false) ||
                    currentEmp.DateConfirmed.HasValue)
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

            CanRequestTraining = !IsDutyAccount && IsEligible && currentEmp != null;

            var today = DateTime.Today;
            bool isHrManager = User.IsInRole("HR Manager");
            bool isHrOfficer = User.IsInRole("HR Officer");
            bool isBranchManager = User.IsInRole("Branch Manager");
            bool isAreaManager = User.IsInRole("Area Manager");
            bool isDeptHead = User.IsInRole("Department Head");
            bool isManagerOrHr = isHrManager || isHrOfficer || isBranchManager || isAreaManager || isDeptHead;

            var user = await _userManager.GetUserAsync(User);
            if (user == null && !string.IsNullOrEmpty(User.Identity?.Name))
            {
                user = await _userManager.FindByNameAsync(User.Identity.Name) ?? await _userManager.FindByEmailAsync(User.Identity.Name);
            }

            int? currentEmpId = user?.EmployeeId;
            if (!currentEmpId.HasValue && user != null)
            {
                var empMatch = await _context.Employees.FirstOrDefaultAsync(e => 
                    (!string.IsNullOrEmpty(user.Email) && e.Email == user.Email) || 
                    (!string.IsNullOrEmpty(user.EpfNumber) && e.EPFNumber == user.EpfNumber));
                currentEmpId = empMatch?.Id;
            }

            // 1. Fetch raw training records from database
            var rawTrainings = await _context.Trainings
                .Include(t => t.Trainer)
                .Include(t => t.EmployeeTrainings)
                    .ThenInclude(et => et.Employee)
                .OrderBy(t => t.Date)
                .ToListAsync();

            // 2. Role-based scoping
            if (!isManagerOrHr)
            {
                // Regular Employee: ONLY see sessions where directly enrolled as an attendee
                if (currentEmpId.HasValue)
                {
                    rawTrainings = rawTrainings
                        .Where(t => t.EmployeeTrainings.Any(et => et.EmployeeId == currentEmpId.Value))
                        .ToList();
                }
                else
                {
                    rawTrainings = new List<HRMS.Domain.Entities.Training.Training>();
                }
            }
            else if (!isHrManager)
            {
                // Branch/Area/Dept Managers or HR Officers: Scoped to their assigned branches
                var allowedBranchIds = await GetAllowedBranchIdsAsync();
                var allowedBranches = await _context.Branches
                    .Where(b => allowedBranchIds.Contains(b.Id))
                    .ToListAsync();

                var branchNames = allowedBranches.Select(b => b.Name).Where(n => !string.IsNullOrWhiteSpace(n)).ToList();
                var branchLocations = allowedBranches.Select(b => b.Location).Where(l => !string.IsNullOrWhiteSpace(l)).ToList();

                rawTrainings = rawTrainings.Where(t =>
                    // A. Has attendees from user's assigned branches
                    t.EmployeeTrainings.Any(et => et.Employee != null && allowedBranchIds.Contains(et.Employee.BranchId)) ||
                    // B. Current user is directly enrolled
                    (currentEmpId.HasValue && t.EmployeeTrainings.Any(et => et.EmployeeId == currentEmpId.Value)) ||
                    // C. Session venue or description matches assigned branch name/location
                    branchNames.Any(bn => (!string.IsNullOrWhiteSpace(t.Location) && t.Location.Contains(bn, StringComparison.OrdinalIgnoreCase)) ||
                                          (!string.IsNullOrWhiteSpace(t.Description) && t.Description.Contains(bn, StringComparison.OrdinalIgnoreCase))) ||
                    branchLocations.Any(bl => (!string.IsNullOrWhiteSpace(t.Location) && t.Location.Contains(bl, StringComparison.OrdinalIgnoreCase)))
                ).ToList();
            }

            var sessionIds = rawTrainings.Select(t => t.Id).ToList();
            var allFeedbacks = await _context.TrainingFeedbacks
                .Where(f => sessionIds.Contains(f.TrainingId))
                .ToListAsync();

            var feedbackGrouped = allFeedbacks
                .GroupBy(f => f.TrainingId)
                .ToDictionary(
                    g => g.Key,
                    g => new {
                        Count = g.Count(),
                        Avg = Math.Round(g.Average(f => (double)f.Rating), 1)
                    }
                );

            var userFeedbacks = currentEmpId.HasValue 
                ? allFeedbacks
                    .Where(f => f.EmployeeId == currentEmpId.Value)
                    .ToDictionary(f => f.TrainingId)
                : new Dictionary<int, HRMS.Domain.Entities.Training.TrainingFeedback>();

            var allSessions = rawTrainings
                .Select(t => {
                    bool hasFb = userFeedbacks.TryGetValue(t.Id, out var fb);
                    int? fbRating = hasFb && fb != null ? fb.Rating : null;
                    bool isEnrolled = currentEmpId.HasValue && t.EmployeeTrainings.Any(et => et.EmployeeId == currentEmpId.Value);

                    feedbackGrouped.TryGetValue(t.Id, out var fbStats);
                    double? avgRating = fbStats != null && fbStats.Count > 0 ? fbStats.Avg : null;
                    int fbCount = fbStats != null ? fbStats.Count : 0;

                    return new ScheduledSessionDto
                    {
                        Id = t.Id,
                        Title = t.Title,
                        Date = t.Date,
                        StartTime = t.StartTime,
                        Trainer = !string.IsNullOrWhiteSpace(t.TrainerName) 
                            ? t.TrainerName 
                            : (t.Trainer != null ? t.Trainer.Name : "N/A"),
                        Location = t.Location ?? "N/A",
                        Status = t.Status ?? "Scheduled",
                        AttendeeCount = t.EmployeeTrainings.Count,
                        HasUserFeedback = hasFb,
                        UserRating = fbRating,
                        IsUserEnrolled = isEnrolled,
                        AverageRating = avgRating,
                        FeedbackCount = fbCount
                    };
                })
                .ToList();

            UpcomingSessions = allSessions
                .Where(s => s.Date.Date >= today && s.Status != "Cancelled" && s.Status != "Completed")
                .OrderBy(s => s.Date)
                .ToList();

            PastSessions = allSessions
                .Where(s => s.Date.Date < today || s.Status == "Cancelled" || s.Status == "Completed")
                .OrderByDescending(s => s.Date)
                .ToList();

            // 3. Fetch history of training requests made by this employee
            var reqEmpId = currentEmp?.Id ?? currentEmpId;
            if (reqEmpId.HasValue)
            {
                MyTrainingRequests = await _context.TrainingProgramRequests
                    .Where(r => r.EmployeeId == reqEmpId.Value)
                    .OrderByDescending(r => r.RequestedDate)
                    .Select(r => new EmployeeTrainingRequestDto
                    {
                        Id = r.Id,
                        Title = r.Title ?? "N/A",
                        Description = r.Description ?? "",
                        RequestedDate = r.RequestedDate,
                        Status = r.Status ?? "Pending"
                    })
                    .ToListAsync();
            }

            return Page();
        }

        public async Task<IActionResult> OnPostRequestTrainingAsync()
        {
            if (CheckIsDutyAccount())
            {
                TempData["ErrorMessage"] = "Duty accounts are not permitted to submit training session requests.";
                return RedirectToPage("./Sessions");
            }

            var employee = await ResolveCurrentEmployeeAsync();
            if (employee == null)
            {
                TempData["ErrorMessage"] = "Unable to identify your employee profile. Duty accounts or unlinked profiles cannot submit training requests.";
                return RedirectToPage("./Sessions");
            }

            bool eligible = string.Equals(employee.EmployeeType, "Permanent", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(employee.Status, "Permanent", StringComparison.OrdinalIgnoreCase) ||
                            (employee.EmployeeType?.Contains("Permanent", StringComparison.OrdinalIgnoreCase) ?? false) ||
                            employee.DateConfirmed.HasValue;

            if (!eligible)
            {
                TempData["ErrorMessage"] = "Training requests are restricted to permanent staff members only.";
                return RedirectToPage("./Sessions");
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
                TempData["ErrorMessage"] = "Please select or specify a valid training program name.";
                return RedirectToPage("./Sessions");
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
                return RedirectToPage("./Sessions");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Failed to submit training request: " + ex.Message;
                return RedirectToPage("./Sessions");
            }
        }

        public async Task<IActionResult> OnPostUpdateStatusAsync(int id, string status)
        {
            if (User.IsInRole("Admin") || User.IsInRole("HR Manager") || User.IsInRole("HR Officer")) return Forbid();

            var training = await _context.Trainings.FindAsync(id);
            if (training != null)
            {
                if (string.Equals(status, "Completed", StringComparison.OrdinalIgnoreCase) && training.Date.Date >= DateTime.Today)
                {
                    TempData["ErrorMessage"] = "Training sessions can only be marked as Completed after the scheduled session date has passed (Session History).";
                    return RedirectToPage("./Sessions");
                }

                training.Status = status;
                await _context.SaveChangesAsync();

                await _trainingNotificationService.NotifySessionStatusChangedAsync(training.Id, status);
                TempData["SuccessMessage"] = $"Training session status updated to '{status}'.";
            }

            return RedirectToPage("./Sessions");
        }
    }

    public class ScheduledSessionDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public TimeSpan StartTime { get; set; }
        public string Trainer { get; set; } = string.Empty; 
        public string Location { get; set; } = string.Empty; 
        public string Status { get; set; } = "Scheduled";
        public int AttendeeCount { get; set; }
        public bool HasUserFeedback { get; set; }
        public int? UserRating { get; set; }
        public bool IsUserEnrolled { get; set; }
        public double? AverageRating { get; set; }
        public int FeedbackCount { get; set; }
    }

    public class EmployeeTrainingRequestDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime RequestedDate { get; set; }
        public string Status { get; set; } = "Pending";
    }
}
