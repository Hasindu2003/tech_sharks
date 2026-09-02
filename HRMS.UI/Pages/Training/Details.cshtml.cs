using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using HRMS.Infrastructure.Identity;
using HRMS.Infrastructure.Persistence;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;

using HRMS.Application.Services;

namespace HRMS.UI.Pages.Training
{
    public class TrainingRequestDetailsDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime RequestedDate { get; set; }
        public string EmployeeName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
    }

    [Authorize(Roles = "Area Manager, Branch Manager")]
    public class DetailsModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ITrainingNotificationService _trainingNotificationService;

        public DetailsModel(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            ITrainingNotificationService trainingNotificationService)
        {
            _context = context;
            _userManager = userManager;
            _trainingNotificationService = trainingNotificationService;
        }

        public TrainingRequestDetailsDto? RequestDetails { get; set; }

        private async Task<List<int>> GetAllowedBranchIdsAsync()
        {
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
            if (User.IsInRole("Admin") || User.IsInRole("HR Manager") || User.IsInRole("HR Officer")) return Forbid();
            if (!id.HasValue || id.Value <= 0) return RedirectToPage("./Manage");

            var allowedBranchIds = await GetAllowedBranchIdsAsync();

            var query = _context.TrainingProgramRequests
                .Include(r => r.Employee)
                .Where(r => r.Id == id.Value && r.Employee != null && allowedBranchIds.Contains(r.Employee.BranchId));

            var req = await query.FirstOrDefaultAsync();

            if (req == null)
            {
                TempData["ErrorMessage"] = "Training request not found or not authorized for your branch jurisdiction.";
                return RedirectToPage("./Manage");
            }

            RequestDetails = new TrainingRequestDetailsDto
            {
                Id = req.Id,
                Title = req.Title ?? "N/A",
                Description = req.Description ?? "",
                Status = req.Status ?? "Pending",
                RequestedDate = req.RequestedDate,
                EmployeeName = req.Employee?.FullName ?? "Unknown",
                Email = req.Employee?.Email ?? ""
            };

            return Page();
        }

        public async Task<IActionResult> OnPostUpdateStatusAsync(int id, string status)
        {
            if (User.IsInRole("Admin") || User.IsInRole("HR Manager") || User.IsInRole("HR Officer")) return Forbid();

            var allowedBranchIds = await GetAllowedBranchIdsAsync();

            var query = _context.TrainingProgramRequests
                .Include(r => r.Employee)
                .Where(r => r.Id == id && r.Employee != null && allowedBranchIds.Contains(r.Employee.BranchId));

            var request = await query.FirstOrDefaultAsync();
            if (request != null)
            {
                request.Status = status;
                await _context.SaveChangesAsync();

                await _trainingNotificationService.NotifyTrainingRequestDecisionAsync(request.Id, status);

                TempData["SuccessMessage"] = $"Request marked as {status}.";
            }

            return RedirectToPage("./Manage");
        }
    }
}
