using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HRMS.Application.Interfaces;
using HRMS.Domain.Entities.Core;
using HRMS.Domain.Entities.Recruitment;
using HRMS.Infrastructure.Identity;
using HRMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HRMS.UI.Pages.CVBank
{
    [Authorize(Roles = "HR Manager, HR Officer, Area Manager, Branch Manager")]
    public class CreateJobModel : PageModel
    {
        private readonly ICVBankService _cvService;
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public CreateJobModel(
            ICVBankService cvService, 
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _cvService = cvService;
            _context = context;
            _userManager = userManager;
        }

        [BindProperty]
        public JobOpening JobInput { get; set; } = new JobOpening();

        public List<Branch> Branches { get; set; } = new();
        public List<Department> Departments { get; set; } = new();
        public List<string> DesignationSuggestions { get; set; } = new();
        public bool IsBranchRestricted { get; set; } = false;

        public async Task OnGetAsync()
        {
            await LoadFormDependenciesAsync();
        }

        private async Task<List<int>?> GetAllowedBranchIdsAsync()
        {
            // HR Manager & Admin have unrestricted organization-wide access
            if (User.IsInRole("Admin") || User.IsInRole("HR Manager"))
            {
                return null;
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
                    if (int.TryParse(token, out var bId) && bId > 0 && !allowedBranchIds.Contains(bId))
                    {
                        allowedBranchIds.Add(bId);
                    }
                }
            }

            if (!allowedBranchIds.Any() && user != null)
            {
                var emp = await _context.Employees.FirstOrDefaultAsync(e => e.Email == user.Email || (user.EmployeeId.HasValue && e.Id == user.EmployeeId.Value));
                if (emp != null && emp.BranchId > 0)
                {
                    allowedBranchIds.Add(emp.BranchId);
                }
            }

            return allowedBranchIds;
        }

        private async Task LoadFormDependenciesAsync()
        {
            var allowedBranchIds = await GetAllowedBranchIdsAsync();
            var allBranches = await _context.Branches.OrderBy(b => b.Name).ToListAsync();

            if (allowedBranchIds != null)
            {
                IsBranchRestricted = true;
                Branches = allBranches.Where(b => allowedBranchIds.Contains(b.Id)).ToList();
                if (Branches.Count == 1 && (!JobInput.BranchId.HasValue || JobInput.BranchId.Value <= 0))
                {
                    JobInput.BranchId = Branches[0].Id;
                }
            }
            else
            {
                IsBranchRestricted = false;
                Branches = allBranches;
            }

            Departments = await _context.Departments.OrderBy(d => d.Name).ToListAsync();

            var defaultDesignations = new List<string>
            {
                "Accountant",
                "Credit Officer",
                "Investment Analyst",
                "Branch Manager",
                "Finance Assistant",
                "HR Officer",
                "Operations Executive",
                "IT Support Specialist",
                "Internal Audit Officer",
                "Customer Service Officer"
            };

            try
            {
                var dbTitles = await _context.Designations
                    .Where(d => !string.IsNullOrEmpty(d.Title))
                    .Select(d => d.Title)
                    .Distinct()
                    .ToListAsync();

                DesignationSuggestions = defaultDesignations.Union(dbTitles).OrderBy(p => p).ToList();
            }
            catch
            {
                DesignationSuggestions = defaultDesignations.OrderBy(p => p).ToList();
            }
        }

        public async Task<IActionResult> OnPostAsync()
        {
            ModelState.Remove("JobInput.JobCode");
            ModelState.Remove("JobInput.Status");
            ModelState.Remove("JobInput.CreatedByUserId");

            if (string.IsNullOrWhiteSpace(JobInput.Title))
            {
                ModelState.AddModelError("JobInput.Title", "Job Title / Position is required.");
            }

            var allowedBranchIds = await GetAllowedBranchIdsAsync();
            if (allowedBranchIds != null)
            {
                IsBranchRestricted = true;
                if (!allowedBranchIds.Any())
                {
                    ModelState.AddModelError("JobInput.BranchId", "No branches are currently assigned to your account. Please contact your HR Manager.");
                }
                else if (!JobInput.BranchId.HasValue || JobInput.BranchId.Value <= 0)
                {
                    ModelState.AddModelError("JobInput.BranchId", "Please select one of your assigned branches.");
                }
                else if (!allowedBranchIds.Contains(JobInput.BranchId.Value))
                {
                    ModelState.AddModelError("JobInput.BranchId", "You are only authorized to open job positions for branches assigned to your account.");
                }
            }

            if (JobInput.MinimumExperienceYears < 0 || JobInput.MinimumExperienceYears > 40)
            {
                ModelState.AddModelError("JobInput.MinimumExperienceYears", "Minimum experience must be between 0 and 40 years.");
            }

            if (JobInput.ClosingDate.HasValue && JobInput.ClosingDate.Value.Date < DateTime.Today)
            {
                ModelState.AddModelError("JobInput.ClosingDate", "Closing date cannot be in the past.");
            }

            if (!ModelState.IsValid)
            {
                await LoadFormDependenciesAsync();
                return Page();
            }

            try
            {
                JobInput.Title = JobInput.Title.Trim();
                JobInput.Status = "Open";
                JobInput.CreatedDate = DateTime.Now;
                JobInput.CreatedByUserId = User.Identity?.Name ?? "HR";

                if (string.IsNullOrWhiteSpace(JobInput.MinimumEducationLevel))
                {
                    JobInput.MinimumEducationLevel = "None";
                }

                if (!string.IsNullOrWhiteSpace(JobInput.RequiredSkills))
                {
                    JobInput.RequiredSkills = JobInput.RequiredSkills.Trim();
                }

                await _cvService.AddJobOpeningAsync(JobInput);

                TempData["SuccessMessage"] = $"Job Opening '{JobInput.Title}' (Ref: {JobInput.JobCode}) was successfully published. Dedicated QR Code is now active!";
                return RedirectToPage("Index");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, "Failed to create job opening: " + ex.Message);
                await LoadFormDependenciesAsync();
                return Page();
            }
        }
    }
}
