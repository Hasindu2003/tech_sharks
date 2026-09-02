using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using HRMS.Infrastructure.Identity;
using HRMS.Infrastructure.Persistence;
using HRMS.Domain.Entities.Training;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using HRMS.Application.Services;

namespace HRMS.UI.Pages.Training
{
    public class SessionAttendeeDto
    {
        public int EmployeeId { get; set; }
        public string NameWithInitials { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string EPFNumber { get; set; } = string.Empty;
        public string BranchName { get; set; } = string.Empty;
        public string DepartmentName { get; set; } = string.Empty;
        public string DesignationName { get; set; } = string.Empty;
        public string EmployeeType { get; set; } = string.Empty;
        public string AttendanceStatus { get; set; } = "Scheduled";
        public string? Score { get; set; }
    }

    public class SessionFeedbackDto
    {
        public int Id { get; set; }
        public int EmployeeId { get; set; }
        public string EmployeeName { get; set; } = string.Empty;
        public string DepartmentName { get; set; } = string.Empty;
        public string DesignationName { get; set; } = string.Empty;
        public int Rating { get; set; }
        public string Comments { get; set; } = string.Empty;
        public DateTime SubmissionDate { get; set; }
    }

    public class SessionDetailsViewDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public TimeSpan StartTime { get; set; }
        public int DurationHours { get; set; }
        public string TrainerName { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public string Status { get; set; } = "Scheduled";
        public List<SessionAttendeeDto> Attendees { get; set; } = new();
    }

    [Authorize]
    public class SessionDetailsModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ITrainingNotificationService _trainingNotificationService;

        public SessionDetailsModel(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            ITrainingNotificationService trainingNotificationService)
        {
            _context = context;
            _userManager = userManager;
            _trainingNotificationService = trainingNotificationService;
        }

        public SessionDetailsViewDto? SessionDetails { get; set; }

        // Participant Feedback Properties
        public List<SessionFeedbackDto> Feedbacks { get; set; } = new();
        public bool IsEnrolledAttendee { get; set; }
        public bool HasSubmittedFeedback { get; set; }
        public SessionFeedbackDto? UserFeedback { get; set; }
        public double AverageRating { get; set; }
        public int TotalFeedbackCount { get; set; }

        public static string FormatNameWithInitials(string? fullName, string? initials)
        {
            if (!string.IsNullOrWhiteSpace(initials))
            {
                return initials.Trim();
            }

            if (!string.IsNullOrWhiteSpace(fullName))
            {
                var parts = fullName.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 1)
                {
                    var initPart = string.Join(" ", parts.Take(parts.Length - 1).Select(p => p[0].ToString().ToUpper() + "."));
                    var lastName = parts[^1];
                    return $"{initPart} {lastName}";
                }
                return parts[0];
            }

            return "Unknown";
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

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (User.IsInRole("Admin")) return Forbid();
            if (!id.HasValue || id.Value <= 0) return RedirectToPage("./Sessions");

            var training = await _context.Trainings
                .Include(t => t.Trainer)
                .Include(t => t.EmployeeTrainings)
                    .ThenInclude(et => et.Employee)
                        .ThenInclude(e => e.Branch)
                .Include(t => t.EmployeeTrainings)
                    .ThenInclude(et => et.Employee)
                        .ThenInclude(e => e.Department)
                .Include(t => t.EmployeeTrainings)
                    .ThenInclude(et => et.Employee)
                        .ThenInclude(e => e.Designation)
                .FirstOrDefaultAsync(t => t.Id == id.Value);

            if (training == null)
            {
                TempData["ErrorMessage"] = "Training session not found.";
                return RedirectToPage("./Sessions");
            }

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

            if (!isManagerOrHr)
            {
                // Regular Employee: ONLY authorized if directly enrolled as an attendee
                bool isEnrolled = currentEmpId.HasValue && training.EmployeeTrainings.Any(et => et.EmployeeId == currentEmpId.Value);
                if (!isEnrolled)
                {
                    TempData["ErrorMessage"] = "You can only view details for training sessions that are assigned to you.";
                    return RedirectToPage("./Sessions");
                }
            }
            else if (!isHrManager)
            {
                // Branch/Area/Dept Managers or HR Officers: Scoped to their assigned branches
                var allowedBranchIds = await GetAllowedBranchIdsAsync();
                var allowedBranches = await _context.Branches.Where(b => allowedBranchIds.Contains(b.Id)).ToListAsync();
                var branchNames = allowedBranches.Select(b => b.Name).Where(n => !string.IsNullOrWhiteSpace(n)).ToList();
                var branchLocations = allowedBranches.Select(b => b.Location).Where(l => !string.IsNullOrWhiteSpace(l)).ToList();

                bool isAuthorized = training.EmployeeTrainings.Any(et => et.Employee != null && allowedBranchIds.Contains(et.Employee.BranchId)) ||
                                    (currentEmpId.HasValue && training.EmployeeTrainings.Any(et => et.EmployeeId == currentEmpId.Value)) ||
                                    branchNames.Any(bn => (!string.IsNullOrEmpty(training.Location) && training.Location.Contains(bn, StringComparison.OrdinalIgnoreCase)) || (!string.IsNullOrEmpty(training.Description) && training.Description.Contains(bn, StringComparison.OrdinalIgnoreCase))) ||
                                    branchLocations.Any(bl => (!string.IsNullOrEmpty(training.Location) && training.Location.Contains(bl, StringComparison.OrdinalIgnoreCase)));

                if (!isAuthorized)
                {
                    TempData["ErrorMessage"] = "You are only authorized to view training sessions for your assigned branches.";
                    return RedirectToPage("./Sessions");
                }
            }

            var attendees = training.EmployeeTrainings
                .Where(et => et.Employee != null)
                .Select(et => new SessionAttendeeDto
                {
                    EmployeeId = et.EmployeeId,
                    NameWithInitials = FormatNameWithInitials(et.Employee.FullName, et.Employee.Initials),
                    FullName = et.Employee.FullName ?? "Unknown",
                    EPFNumber = et.Employee.EPFNumber ?? "N/A",
                    BranchName = et.Employee.Branch != null ? et.Employee.Branch.Name : "Main Branch",
                    DepartmentName = et.Employee.Department != null ? et.Employee.Department.Name : "Unassigned",
                    DesignationName = et.Employee.Designation != null ? et.Employee.Designation.Title : "Staff",
                    EmployeeType = !string.IsNullOrWhiteSpace(et.Employee.EmployeeType) 
                        ? et.Employee.EmployeeType 
                        : (!string.IsNullOrWhiteSpace(et.Employee.Status) ? et.Employee.Status : "General"),
                    AttendanceStatus = et.AttendanceStatus ?? "Scheduled",
                    Score = et.Score
                })
                .OrderBy(a => a.NameWithInitials)
                .ToList();

            SessionDetails = new SessionDetailsViewDto
            {
                Id = training.Id,
                Title = training.Title,
                Description = training.Description,
                Date = training.Date,
                StartTime = training.StartTime,
                DurationHours = training.DurationHours,
                TrainerName = !string.IsNullOrWhiteSpace(training.TrainerName) 
                    ? training.TrainerName 
                    : (training.Trainer != null ? training.Trainer.Name : "External Trainer"),
                Location = training.Location ?? "N/A",
                Status = training.Status ?? "Scheduled",
                Attendees = attendees
            };

            // ── Load Participant Feedback ──
            IsEnrolledAttendee = currentEmpId.HasValue && training.EmployeeTrainings.Any(et => et.EmployeeId == currentEmpId.Value);

            var rawFeedbacks = await _context.TrainingFeedbacks
                .Include(f => f.Employee)
                    .ThenInclude(e => e.Department)
                .Include(f => f.Employee)
                    .ThenInclude(e => e.Designation)
                .Where(f => f.TrainingId == training.Id)
                .OrderByDescending(f => f.SubmissionDate)
                .ToListAsync();

            Feedbacks = rawFeedbacks.Select(f => new SessionFeedbackDto
            {
                Id = f.Id,
                EmployeeId = f.EmployeeId,
                EmployeeName = FormatNameWithInitials(f.Employee?.FullName, f.Employee?.Initials),
                DepartmentName = f.Employee?.Department?.Name ?? "Unassigned",
                DesignationName = f.Employee?.Designation?.Title ?? "Staff",
                Rating = f.Rating,
                Comments = f.Comments ?? "",
                SubmissionDate = f.SubmissionDate
            }).ToList();

            TotalFeedbackCount = Feedbacks.Count;
            AverageRating = TotalFeedbackCount > 0 ? Math.Round(Feedbacks.Average(f => f.Rating), 1) : 0.0;

            if (currentEmpId.HasValue)
            {
                var myFeedback = rawFeedbacks.FirstOrDefault(f => f.EmployeeId == currentEmpId.Value);
                if (myFeedback != null)
                {
                    HasSubmittedFeedback = true;
                    UserFeedback = new SessionFeedbackDto
                    {
                        Id = myFeedback.Id,
                        EmployeeId = myFeedback.EmployeeId,
                        EmployeeName = FormatNameWithInitials(myFeedback.Employee?.FullName, myFeedback.Employee?.Initials),
                        DepartmentName = myFeedback.Employee?.Department?.Name ?? "",
                        DesignationName = myFeedback.Employee?.Designation?.Title ?? "",
                        Rating = myFeedback.Rating,
                        Comments = myFeedback.Comments ?? "",
                        SubmissionDate = myFeedback.SubmissionDate
                    };
                }
            }

            return Page();
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
                    return RedirectToPage("./SessionDetails", new { id });
                }

                training.Status = status;
                await _context.SaveChangesAsync();

                await _trainingNotificationService.NotifySessionStatusChangedAsync(training.Id, status);

                TempData["SuccessMessage"] = $"Session status updated to '{status}'.";
            }

            return RedirectToPage("./SessionDetails", new { id });
        }

        public async Task<IActionResult> OnPostSubmitFeedbackAsync(int trainingId, int rating, string? comments)
        {
            if (User.IsInRole("Admin")) return Forbid();

            var training = await _context.Trainings
                .Include(t => t.EmployeeTrainings)
                .FirstOrDefaultAsync(t => t.Id == trainingId);

            if (training == null)
            {
                TempData["ErrorMessage"] = "Training session not found.";
                return RedirectToPage("./Sessions");
            }

            if (!string.Equals(training.Status, "Completed", StringComparison.OrdinalIgnoreCase))
            {
                TempData["ErrorMessage"] = "Feedback can only be submitted after the training session is marked as Completed.";
                return RedirectToPage("./SessionDetails", new { id = trainingId });
            }

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

            if (!currentEmpId.HasValue || !training.EmployeeTrainings.Any(et => et.EmployeeId == currentEmpId.Value))
            {
                TempData["ErrorMessage"] = "Only enrolled attendees of this training program can submit feedback.";
                return RedirectToPage("./SessionDetails", new { id = trainingId });
            }

            if (rating < 1 || rating > 5)
            {
                TempData["ErrorMessage"] = "Please select a rating between 1 and 5 stars.";
                return RedirectToPage("./SessionDetails", new { id = trainingId });
            }

            var existingFeedback = await _context.TrainingFeedbacks
                .FirstOrDefaultAsync(f => f.TrainingId == trainingId && f.EmployeeId == currentEmpId.Value);

            if (existingFeedback != null)
            {
                existingFeedback.Rating = rating;
                existingFeedback.Comments = comments?.Trim();
                existingFeedback.SubmissionDate = DateTime.Now;
                TempData["SuccessMessage"] = "Your feedback has been updated successfully.";
            }
            else
            {
                var feedback = new TrainingFeedback
                {
                    TrainingId = trainingId,
                    EmployeeId = currentEmpId.Value,
                    Rating = rating,
                    Comments = comments?.Trim(),
                    SubmissionDate = DateTime.Now
                };
                _context.TrainingFeedbacks.Add(feedback);
                TempData["SuccessMessage"] = "Thank you! Your feedback has been submitted successfully.";
            }

            await _context.SaveChangesAsync();
            return RedirectToPage("./SessionDetails", new { id = trainingId });
        }
    }
}
