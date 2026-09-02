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
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HRMS.UI.Pages.Training
{
    [Authorize(Roles = "Area Manager, Branch Manager")]
    public class EditSessionModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ITrainingNotificationService _trainingNotificationService;

        public EditSessionModel(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            ITrainingNotificationService trainingNotificationService)
        {
            _context = context;
            _userManager = userManager;
            _trainingNotificationService = trainingNotificationService;
        }

        [BindProperty]
        public int Id { get; set; }

        [BindProperty]
        public string SelectedProgramTitle { get; set; } = string.Empty;

        [BindProperty]
        public string? CustomProgramTitle { get; set; }

        [BindProperty]
        public string TrainerName { get; set; } = string.Empty;

        [BindProperty]
        public string Location { get; set; } = string.Empty;

        [BindProperty]
        public string? Description { get; set; }

        [BindProperty]
        public DateTime SessionDate { get; set; } = DateTime.Now;

        [BindProperty]
        public TimeSpan StartTimeValue { get; set; } = new TimeSpan(9, 0, 0);

        [BindProperty]
        public string Status { get; set; } = "Scheduled";

        [BindProperty]
        public int SelectedBranchId { get; set; }

        [BindProperty]
        public List<int> SelectedEmployeeIds { get; set; } = new();

        public List<BranchDto> AvailableBranches { get; set; } = new();
        public List<string> AvailableDepartments { get; set; } = new();
        public List<string> AvailableEmployeeTypes { get; set; } = new();
        public List<EmployeeItemDto> BranchEmployees { get; set; } = new();
        public List<string> ApprovedPrograms { get; set; } = new();

        private async Task<List<BranchDto>> GetAllowedBranchesAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null && !string.IsNullOrEmpty(User.Identity?.Name))
            {
                user = await _userManager.FindByNameAsync(User.Identity.Name) ?? await _userManager.FindByEmailAsync(User.Identity.Name);
            }

            if (User.IsInRole("HR Manager"))
            {
                return await _context.Branches
                    .OrderBy(b => b.Name)
                    .Select(b => new BranchDto { Id = b.Id, Name = b.Name, Location = b.Location ?? "" })
                    .ToListAsync();
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
                var branchMatch = await _context.Branches.FirstOrDefaultAsync(b => b.Name == user.Branch);
                if (branchMatch != null)
                {
                    allowedBranchIds.Add(branchMatch.Id);
                }
            }

            if (!allowedBranchIds.Any() && user?.EmployeeId.HasValue == true)
            {
                var emp = await _context.Employees.FirstOrDefaultAsync(e => e.Id == user.EmployeeId.Value);
                if (emp != null)
                {
                    allowedBranchIds.Add(emp.BranchId);
                }
            }

            if (allowedBranchIds.Any())
            {
                return await _context.Branches
                    .Where(b => allowedBranchIds.Distinct().Contains(b.Id))
                    .OrderBy(b => b.Name)
                    .Select(b => new BranchDto { Id = b.Id, Name = b.Name, Location = b.Location ?? "" })
                    .ToListAsync();
            }

            return new List<BranchDto>();
        }

        private static string FormatNameWithInitials(string? fullName, string? initials)
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

        private async Task<(HashSet<int> DutyEmployeeIds, HashSet<string> DutyIdentifiers)> GetDutyAccountExclusionsAsync()
        {
            var dutyEmployeeIds = new HashSet<int>();
            var dutyIdentifiers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var dutyRoles = new[] { "Admin", "HR Manager", "HR Officer", "Branch Manager", "Area Manager", "Department Head" };
            foreach (var role in dutyRoles)
            {
                var usersInRole = await _userManager.GetUsersInRoleAsync(role);
                foreach (var u in usersInRole)
                {
                    if (u.EmployeeId.HasValue && u.EmployeeId.Value > 0)
                        dutyEmployeeIds.Add(u.EmployeeId.Value);

                    if (!string.IsNullOrWhiteSpace(u.Email))
                        dutyIdentifiers.Add(u.Email.Trim());

                    if (!string.IsNullOrWhiteSpace(u.UserName))
                        dutyIdentifiers.Add(u.UserName.Trim());

                    if (!string.IsNullOrWhiteSpace(u.EpfNumber))
                        dutyIdentifiers.Add(u.EpfNumber.Trim());
                }
            }

            return (dutyEmployeeIds, dutyIdentifiers);
        }

        private async Task PopulateDataAsync()
        {
            AvailableBranches = await GetAllowedBranchesAsync();
            var allowedBranchIds = AvailableBranches.Select(b => b.Id).ToList();

            ApprovedPrograms = new List<string>
            {
                "Gold Loan Appraising",
                "Credit Evaluation & Lending",
                "Leasing & Hire Purchase Operations",
                "Debt Recovery & Negotiation Skills",
                "AML & KYC Compliance",
                "Financial Fraud Detection",
                "Customer Service Excellence",
                "Financial Product Sales & Marketing",
                "Core Banking System (CBS) Training",
                "Cybersecurity & Data Privacy",
                "Advanced Microsoft Excel",
                "Microfinance Field Best Practices",
                "Workplace Ethics & Conduct",
                "Strategic Leadership & Team Management",
                "IT Infrastructure & Troubleshooting"
            };

            if (!string.IsNullOrWhiteSpace(SelectedProgramTitle) && !ApprovedPrograms.Contains(SelectedProgramTitle))
            {
                ApprovedPrograms.Insert(0, SelectedProgramTitle);
            }

            if (allowedBranchIds.Any())
            {
                var (dutyEmployeeIds, dutyIdentifiers) = await GetDutyAccountExclusionsAsync();

                var rawEmployees = await _context.Employees
                    .Where(e => allowedBranchIds.Contains(e.BranchId) 
                             && e.Status != "Terminated" 
                             && e.Status != "Resigned" 
                             && e.Status != "Deceased"
                             && e.NIC != "DUTY-ACC"
                             && !e.NIC.StartsWith("DUTY")
                             && !dutyEmployeeIds.Contains(e.Id))
                    .Include(e => e.Branch)
                    .Include(e => e.Department)
                    .Include(e => e.Designation)
                    .OrderBy(e => e.FullName)
                    .ToListAsync();

                BranchEmployees = rawEmployees
                    .Where(e => (string.IsNullOrEmpty(e.Email) || !dutyIdentifiers.Contains(e.Email.Trim()))
                             && (string.IsNullOrEmpty(e.EPFNumber) || !dutyIdentifiers.Contains(e.EPFNumber.Trim()))
                             && !(e.FullName != null && e.FullName.Contains("Duty", StringComparison.OrdinalIgnoreCase)))
                    .Select(e => new EmployeeItemDto
                    {
                        Id = e.Id,
                        NameWithInitials = FormatNameWithInitials(e.FullName, e.Initials),
                        FullName = e.FullName ?? "Unknown",
                        EPFNumber = e.EPFNumber ?? "",
                        BranchId = e.BranchId,
                        BranchName = e.Branch != null ? e.Branch.Name : "",
                        DepartmentId = e.DepartmentId,
                        DepartmentName = e.Department != null ? e.Department.Name : "Unassigned",
                        DesignationName = e.Designation != null ? e.Designation.Title : "Staff",
                        EmployeeType = !string.IsNullOrWhiteSpace(e.EmployeeType) ? e.EmployeeType : (!string.IsNullOrWhiteSpace(e.Status) ? e.Status : "General"),
                        Status = e.Status ?? "Active"
                    })
                    .ToList();

                AvailableDepartments = BranchEmployees
                    .Select(e => e.DepartmentName)
                    .Where(d => !string.IsNullOrWhiteSpace(d))
                    .Distinct()
                    .OrderBy(d => d)
                    .ToList();

                AvailableEmployeeTypes = BranchEmployees
                    .Select(e => e.EmployeeType)
                    .Where(t => !string.IsNullOrWhiteSpace(t))
                    .Distinct()
                    .OrderBy(t => t)
                    .ToList();
            }
        }

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (User.IsInRole("Admin") || User.IsInRole("HR Manager") || User.IsInRole("HR Officer")) return Forbid();
            if (!id.HasValue || id.Value <= 0) return RedirectToPage("./Sessions");

            var training = await _context.Trainings
                .Include(t => t.Trainer)
                .Include(t => t.EmployeeTrainings)
                .FirstOrDefaultAsync(t => t.Id == id.Value);

            if (training == null)
            {
                TempData["ErrorMessage"] = "Training session not found.";
                return RedirectToPage("./Sessions");
            }

            Id = training.Id;
            SelectedProgramTitle = training.Title;
            TrainerName = !string.IsNullOrWhiteSpace(training.TrainerName) 
                ? training.TrainerName 
                : (training.Trainer != null ? training.Trainer.Name : "");
            Location = training.Location ?? "";
            Description = training.Description;
            SessionDate = training.Date;
            StartTimeValue = training.StartTime;
            Status = training.Status ?? "Scheduled";

            SelectedEmployeeIds = training.EmployeeTrainings.Select(et => et.EmployeeId).ToList();

            await PopulateDataAsync();

            // Try to resolve selected branch from existing employee attendees or available branches
            if (SelectedBranchId == 0)
            {
                if (training.EmployeeTrainings.Any())
                {
                    var firstEmpId = training.EmployeeTrainings.First().EmployeeId;
                    var emp = await _context.Employees.FirstOrDefaultAsync(e => e.Id == firstEmpId);
                    if (emp != null)
                    {
                        SelectedBranchId = emp.BranchId;
                    }
                }

                if (SelectedBranchId == 0 && AvailableBranches.Any())
                {
                    SelectedBranchId = AvailableBranches[0].Id;
                }
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (User.IsInRole("Admin") || User.IsInRole("HR Manager") || User.IsInRole("HR Officer")) return Forbid();

            var training = await _context.Trainings
                .Include(t => t.EmployeeTrainings)
                .FirstOrDefaultAsync(t => t.Id == Id);

            if (training == null)
            {
                TempData["ErrorMessage"] = "Training session not found.";
                return RedirectToPage("./Sessions");
            }

            AvailableBranches = await GetAllowedBranchesAsync();
            var allowedBranchIds = AvailableBranches.Select(b => b.Id).ToList();

            if (SelectedBranchId > 0 && !allowedBranchIds.Contains(SelectedBranchId))
            {
                ModelState.AddModelError("SelectedBranchId", "You are only authorized to manage sessions for your assigned branches.");
            }

            var programTitle = SelectedProgramTitle;
            if (string.Equals(programTitle, "Other", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(programTitle, "Custom", StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(programTitle))
            {
                programTitle = CustomProgramTitle;
            }

            if (string.IsNullOrWhiteSpace(programTitle))
            {
                ModelState.AddModelError("SelectedProgramTitle", "Please select or type a training program title.");
            }

            if (string.Equals(Status, "Completed", StringComparison.OrdinalIgnoreCase) && SessionDate.Date >= DateTime.Today)
            {
                ModelState.AddModelError("Status", "Training sessions can only be marked as Completed after the scheduled session date has passed (Session History).");
            }

            if (!ModelState.IsValid)
            {
                await PopulateDataAsync();
                return Page();
            }

            try
            {
                var branch = AvailableBranches.FirstOrDefault(b => b.Id == SelectedBranchId);
                var branchName = branch?.Name ?? "Branch Office";

                training.Title = programTitle!.Trim();
                training.Description = !string.IsNullOrWhiteSpace(Description) 
                    ? Description 
                    : $"{programTitle!.Trim()} for {branchName} conducted by {TrainerName}";
                training.Date = SessionDate.Date;
                training.StartTime = StartTimeValue;
                training.TrainerName = TrainerName;
                training.Location = !string.IsNullOrWhiteSpace(Location) ? Location : branchName;
                training.Status = Status;

                // Sync Employee Attendees
                var selectedSet = (SelectedEmployeeIds ?? new List<int>()).ToHashSet();
                var existingAttendees = training.EmployeeTrainings.ToList();

                // 1. Remove unselected attendees
                var toRemove = existingAttendees.Where(et => !selectedSet.Contains(et.EmployeeId)).ToList();
                foreach (var rem in toRemove)
                {
                    _context.EmployeeTrainings.Remove(rem);
                }

                // 2. Add new attendees
                var existingEmpIds = existingAttendees.Select(et => et.EmployeeId).ToHashSet();
                var toAdd = selectedSet.Where(empId => !existingEmpIds.Contains(empId)).ToList();

                foreach (var empId in toAdd)
                {
                    _context.EmployeeTrainings.Add(new EmployeeTraining
                    {
                        TrainingId = training.Id,
                        EmployeeId = empId,
                        AttendanceStatus = "Scheduled"
                    });
                }

                await _context.SaveChangesAsync();

                await _trainingNotificationService.NotifySessionUpdatedAsync(training.Id, selectedSet.ToList());

                TempData["SuccessMessage"] = $"Training session '{training.Title}' updated successfully ({selectedSet.Count} employees assigned).";
                return RedirectToPage("./SessionDetails", new { id = training.Id });
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Failed to update training session: " + ex.Message);
                await PopulateDataAsync();
                return Page();
            }
        }
    }
}
