using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using HRMS.Infrastructure.Identity;
using HRMS.Infrastructure.Persistence;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using System;

namespace HRMS.UI.Pages.Training
{
    [Authorize(Roles = "Area Manager, Branch Manager")]
    public class ManageModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public ManageModel(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public class RequestView
        {
            public int Id { get; set; }
            public string EmployeeName { get; set; } = string.Empty;
            public string BranchName { get; set; } = string.Empty;
            public string ProgramName { get; set; } = string.Empty;
            public string Status { get; set; } = string.Empty;
            public string Date { get; set; } = string.Empty;
        }

        public List<RequestView> PendingRequests { get; set; } = new();
        public List<RequestView> ReviewedRequests { get; set; } = new();

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

        public async Task<IActionResult> OnGetAsync()
        {
            if (User.IsInRole("Admin") || User.IsInRole("HR Manager") || User.IsInRole("HR Officer")) return Forbid();

            var allowedBranchIds = await GetAllowedBranchIdsAsync();

            var query = _context.TrainingProgramRequests
                .Include(r => r.Employee)
                    .ThenInclude(e => e.Branch)
                .Where(r => r.Employee != null && allowedBranchIds.Contains(r.Employee.BranchId));

            var data = await query
                .OrderByDescending(r => r.Id)
                .Select(r => new RequestView
                {
                    Id = r.Id,
                    EmployeeName = r.Employee != null ? r.Employee.FullName : "Unknown",
                    BranchName = r.Employee != null && r.Employee.Branch != null ? r.Employee.Branch.Name : "Branch",
                    ProgramName = r.Title ?? "N/A",
                    Status = r.Status ?? "Pending",
                    Date = r.RequestedDate.ToString("yyyy-MM-dd")
                })
                .ToListAsync();

            PendingRequests = data
                .Where(r => string.Equals(r.Status, "Pending", StringComparison.OrdinalIgnoreCase))
                .ToList();

            ReviewedRequests = data
                .Where(r => !string.Equals(r.Status, "Pending", StringComparison.OrdinalIgnoreCase))
                .ToList();

            return Page();
        }
    }
}
