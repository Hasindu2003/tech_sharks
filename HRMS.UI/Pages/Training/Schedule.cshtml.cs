using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using HRMS.Infrastructure.Identity;
using HRMS.Infrastructure.Persistence;
using HRMS.Domain.Entities.Core;
using HRMS.Domain.Entities.Training;
using HRMS.Domain.Common;
using HRMS.Application.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HRMS.UI.Pages.Training
{
    public class BranchDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
    }

    public class EmployeeItemDto
    {
        public int Id { get; set; }
        public string NameWithInitials { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string EPFNumber { get; set; } = string.Empty;
        public int BranchId { get; set; }
        public string BranchName { get; set; } = string.Empty;
        public int? DepartmentId { get; set; }
        public string DepartmentName { get; set; } = string.Empty;
        public string DesignationName { get; set; } = string.Empty;
        public string EmployeeType { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }

    [Authorize(Roles = "Area Manager, Branch Manager")]
    public class ScheduleModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ITrainingNotificationService _trainingNotificationService;

        public ScheduleModel(
            ApplicationDbContext context, 
            UserManager<ApplicationUser> userManager,
            ITrainingNotificationService trainingNotificationService)
        {
            _context = context;
            _userManager = userManager;
            _trainingNotificationService = trainingNotificationService;
        }

        [BindProperty]
        public string SelectedProgramTitle { get; set; } = string.Empty;

        [BindProperty]
        public string? CustomProgramTitle { get; set; }

        [BindProperty]
        public string TrainerName { get; set; } = string.Empty;

        [BindProperty]
        public string Location { get; set; } = string.Empty;

        [BindProperty]
        public DateTime SessionDate { get; set; } = SriLankaTime.Now.Date.AddDays(1);

        [BindProperty]
        public TimeSpan StartTimeValue { get; set; } = new TimeSpan(9, 0, 0);

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

            // HR Manager has access to all branches
            if (User.IsInRole("HR Manager"))
            {
                return await _context.Branches
                    .OrderBy(b => b.Name)
                    .Select(b => new BranchDto { Id = b.Id, Name = b.Name, Location = b.Location ?? "" })
                    .ToListAsync();
            }

            // Area Manager / HR Officer / Branch Manager: Scoped to their assigned branches
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

            // Fallback to user's assigned branch name if ManagedBranches is empty or for single branch roles
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

        public async Task<IActionResult> OnGetAsync()
        {
            if (User.IsInRole("Admin") || User.IsInRole("HR Manager") || User.IsInRole("HR Officer")) return Forbid();

            await PopulateDataAsync();

            if (AvailableBranches.Count == 1 && SelectedBranchId == 0)
            {
                SelectedBranchId = AvailableBranches[0].Id;
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (User.IsInRole("Admin") || User.IsInRole("HR Manager") || User.IsInRole("HR Officer")) return Forbid();

            AvailableBranches = await GetAllowedBranchesAsync();
            var allowedBranchIds = AvailableBranches.Select(b => b.Id).ToList();

            if (SelectedBranchId > 0 && !allowedBranchIds.Contains(SelectedBranchId))
            {
                ModelState.AddModelError("SelectedBranchId", "You are only authorized to schedule training for your assigned branches.");
            }

            var minAllowedDate = SriLankaTime.Now.Date.AddDays(1);
            if (SessionDate.Date < minAllowedDate)
            {
                ModelState.AddModelError("SessionDate", "Training session must be scheduled for a future date (tomorrow onwards). Today or past dates are not permitted.");
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
                ModelState.AddModelError("SelectedProgramTitle", "Please select or type a training program.");
            }

            if (!ModelState.IsValid)
            {
                await PopulateDataAsync();
                return Page();
            }

            try
            {
                var branch = AvailableBranches.FirstOrDefault(b => b.Id == SelectedBranchId);
                var branchName = branch != null 
                    ? branch.Name 
                    : (AvailableBranches.Count > 1 
                        ? $"Multi-Branch ({string.Join(", ", AvailableBranches.Take(3).Select(b => b.Name))}{(AvailableBranches.Count > 3 ? "..." : "")})" 
                        : "Branch Office");

                var newTraining = new HRMS.Domain.Entities.Training.Training
                {
                    Title = programTitle!.Trim(),
                    Description = $"{programTitle!.Trim()} for {branchName} conducted by {TrainerName}",
                    Date = SessionDate.Date,
                    StartTime = StartTimeValue,
                    DurationHours = 2,
                    TrainerName = TrainerName,
                    Location = !string.IsNullOrWhiteSpace(Location) ? Location : branchName,
                    Status = "Scheduled"
                };

                _context.Trainings.Add(newTraining);
                await _context.SaveChangesAsync();

                // Assign selected employees from any of the user's allowed branches
                var assignedCount = 0;
                var validEmpIds = new List<int>();
                if (SelectedEmployeeIds != null && SelectedEmployeeIds.Any())
                {
                    validEmpIds = await _context.Employees
                        .Where(e => allowedBranchIds.Contains(e.BranchId) && SelectedEmployeeIds.Contains(e.Id))
                        .Select(e => e.Id)
                        .ToListAsync();

                    foreach (var empId in validEmpIds)
                    {
                        _context.EmployeeTrainings.Add(new EmployeeTraining
                        {
                            TrainingId = newTraining.Id,
                            EmployeeId = empId,
                            AttendanceStatus = "Scheduled"
                        });
                    }
                    await _context.SaveChangesAsync();
                    assignedCount = validEmpIds.Count;
                }

                await _trainingNotificationService.NotifySessionScheduledAsync(newTraining.Id, validEmpIds, SelectedBranchId);

                TempData["SuccessMessage"] = $"Training session '{newTraining.Title}' scheduled successfully with {assignedCount} employees assigned across your branches.";
                return RedirectToPage("./SessionDetails", new { id = newTraining.Id });
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Failed to schedule training session: " + ex.Message);
                await PopulateDataAsync();
                return Page();
            }
        }
    }
}
