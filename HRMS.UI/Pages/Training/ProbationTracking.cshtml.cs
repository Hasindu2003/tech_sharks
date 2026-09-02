using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using HRMS.Infrastructure.Identity;
using HRMS.Infrastructure.Persistence;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;
using System.Data;
using System.Linq;

namespace HRMS.UI.Pages.Training
{
    [Authorize(Roles = "Department Head, Branch Manager, Area Manager, HR Manager, HR Officer")]
    public class ProbationTrackingModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public ProbationTrackingModel(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public List<ProbationDetailDto> ProbationEmployees { get; set; } = new();

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

        public async Task<IActionResult> OnGetAsync()
        {
            if (User.IsInRole("Admin")) return Forbid();

            var isHrManager = User.IsInRole("HR Manager");
            var allowedBranchIds = await GetAllowedBranchIdsAsync();

            var connection = _context.Database.GetDbConnection();
            using (var cmd = connection.CreateCommand())
            {
                if (connection.State != ConnectionState.Open) 
                    await connection.OpenAsync();

                cmd.CommandText = @"SELECT e.Id, e.FullName, e.EPFNumber, e.DateJoined, e.ProbationPeriodMonths, e.BranchId,
                                   b.Name as BranchName, d.Name as DepartmentName,
                                   (SELECT MAX(EvaluationMonth) FROM ProbationEvaluations WHERE EmployeeId = e.Id) as LastMonth
                                   FROM Employees e 
                                   LEFT JOIN Branches b ON e.BranchId = b.Id
                                   LEFT JOIN Departments d ON e.DepartmentId = d.Id
                                   WHERE e.EmployeeType = 'Probation' OR e.EmployeeType = 'Probationary' OR e.Status = 'Probation'
                                   ORDER BY e.FullName ASC";
                
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    ProbationEmployees.Clear();
                    while (await reader.ReadAsync())
                    {
                        int branchId = reader["BranchId"] != DBNull.Value ? Convert.ToInt32(reader["BranchId"]) : 0;
                        if (!isHrManager && !allowedBranchIds.Contains(branchId))
                        {
                            continue;
                        }

                        int lastMonthVal = 0;
                        var monthRaw = reader["LastMonth"];
                        if (monthRaw != DBNull.Value && monthRaw != null)
                        {
                            int.TryParse(monthRaw.ToString(), out lastMonthVal);
                        }

                        int probationMonths = 6;
                        var probPeriodRaw = reader["ProbationPeriodMonths"];
                        if (probPeriodRaw != DBNull.Value && probPeriodRaw != null)
                        {
                            if (int.TryParse(probPeriodRaw.ToString(), out int parsedMonths) && parsedMonths > 0)
                            {
                                probationMonths = parsedMonths;
                            }
                        }

                        DateTime dateJoined = reader["DateJoined"] != DBNull.Value ? Convert.ToDateTime(reader["DateJoined"]) : DateTime.Today;

                        int currentMonthDisplay = lastMonthVal;
                        double progressPct = Math.Min(100.0, ((double)currentMonthDisplay / probationMonths) * 100.0);
                        bool isPendingReview = currentMonthDisplay >= probationMonths;

                        ProbationEmployees.Add(new ProbationDetailDto {
                            Id = Convert.ToInt32(reader["Id"]),
                            FullName = reader["FullName"]?.ToString() ?? string.Empty,
                            EPFNumber = reader["EPFNumber"]?.ToString() ?? string.Empty,
                            BranchName = reader["BranchName"]?.ToString() ?? "Branch Office",
                            DepartmentName = reader["DepartmentName"]?.ToString() ?? "General",
                            DateJoined = dateJoined,
                            CurrentMonth = currentMonthDisplay,
                            TotalProbationMonths = probationMonths,
                            ProgressPercentage = progressPct,
                            Status = isPendingReview ? "Review Pending" : "On Track"
                        });
                    }
                }
            }
            return Page();
        }
    }

    public class ProbationDetailDto {
        public int Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string EPFNumber { get; set; } = string.Empty;
        public string BranchName { get; set; } = string.Empty;
        public string DepartmentName { get; set; } = string.Empty;
        public DateTime DateJoined { get; set; }
        public int CurrentMonth { get; set; }
        public int TotalProbationMonths { get; set; } = 6;
        public double ProgressPercentage { get; set; }
        public string Status { get; set; } = string.Empty;
    }
}
