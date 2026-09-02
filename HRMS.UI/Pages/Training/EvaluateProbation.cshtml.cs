using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using HRMS.Infrastructure.Identity;
using HRMS.Infrastructure.Persistence;
using System.Data;
using System.Data.Common;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;

namespace HRMS.UI.Pages.Training
{
    [Authorize(Roles = "Branch Manager, Area Manager, HR Manager, HR Officer")]
    public class EvaluateProbationModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public EvaluateProbationModel(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        [BindProperty]
        public ProbationEvaluationInput Evaluation { get; set; } = new();
        
        public EmployeeDetailsDto Employee { get; set; } = new();
        public int TotalProbationMonths { get; set; } = 6;
        public List<int> ExistingMonths { get; set; } = new();

        public async Task<IActionResult> OnGetAsync(int id)
        {
            if (User.IsInRole("Admin")) return Forbid();
            var connection = _context.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open) await connection.OpenAsync();

            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = @"SELECT e.Id, e.FullName, d.Name, e.ProbationPeriodMonths 
                                   FROM Employees e 
                                   LEFT JOIN Departments d ON e.DepartmentId = d.Id 
                                   WHERE e.Id = @id";
                
                var param = cmd.CreateParameter();
                param.ParameterName = "@id";
                param.Value = id;
                cmd.Parameters.Add(param);

                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        Employee = new EmployeeDetailsDto
                        {
                            Id = reader.GetInt32(0),
                            FullName = reader.IsDBNull(1) ? "Unknown" : reader.GetString(1),
                            Department = reader.IsDBNull(2) ? "General" : reader.GetString(2)
                        };
                        TotalProbationMonths = (!reader.IsDBNull(3) && reader.GetInt32(3) > 0) ? reader.GetInt32(3) : 6;
                        Evaluation.EmployeeId = id;
                    }
                    else { return RedirectToPage("./Dashboard"); }
                }
            }

            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "SELECT EvaluationMonth FROM ProbationEvaluations WHERE EmployeeId = @id";
                var param = cmd.CreateParameter();
                param.ParameterName = "@id";
                param.Value = id;
                cmd.Parameters.Add(param);

                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        ExistingMonths.Add(reader.GetInt32(0));
                    }
                }
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (User.IsInRole("Admin")) return Forbid();
            if (!ModelState.IsValid) return Page();

            var user = await _userManager.GetUserAsync(User);
            int evalById = user?.EmployeeId ?? 1;
            if (evalById == 1 && user?.Email != null)
            {
                var emp = await _context.Employees.FirstOrDefaultAsync(e => e.Email == user.Email);
                if (emp != null) evalById = emp.Id;
            }

            var connection = _context.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open) await connection.OpenAsync();

            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = @"INSERT INTO ProbationEvaluations 
                    (EmployeeId, EvaluatedBy, EvaluationMonth, PerformanceScore, AttendanceScore, ConductScore, Comments, EvaluationDate) 
                    VALUES (@empId, @evalBy, @month, @perf, @att, @cond, @comm, @date)";

                AddParam(cmd, "@empId", Evaluation.EmployeeId);
                AddParam(cmd, "@evalBy", evalById); 
                AddParam(cmd, "@month", Evaluation.Month);
                AddParam(cmd, "@perf", Evaluation.PerformanceScore);
                AddParam(cmd, "@att", Evaluation.AttendanceScore);
                AddParam(cmd, "@cond", Evaluation.ConductScore);
                AddParam(cmd, "@comm", Evaluation.Comments ?? "");
                AddParam(cmd, "@date", DateTime.Now);

                await cmd.ExecuteNonQueryAsync();
            }

            return RedirectToPage("./Dashboard");
        }

        private void AddParam(DbCommand cmd, string name, object value)
        {
            var param = cmd.CreateParameter();
            param.ParameterName = name;
            param.Value = value ?? DBNull.Value;
            cmd.Parameters.Add(param);
        }
    }

    public class ProbationEvaluationInput
    {
        public int EmployeeId { get; set; }
        public int Month { get; set; }
        public int PerformanceScore { get; set; }
        public int AttendanceScore { get; set; }
        public int ConductScore { get; set; }
        public string? Comments { get; set; }
    }
}
