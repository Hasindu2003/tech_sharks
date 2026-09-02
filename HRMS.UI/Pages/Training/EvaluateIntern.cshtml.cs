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
    public class EmployeeDetailsDto
    {
        public int Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
    }

    [Authorize(Roles = "Department Head, Branch Manager, Area Manager, HR Manager, HR Officer")]
    public class EvaluateInternModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public EvaluateInternModel(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        [BindProperty]
        public InternEvaluationInput Evaluation { get; set; } = new();
        
        public EmployeeDetailsDto Intern { get; set; } = new();
        
        public List<int> ExistingMonths { get; set; } = new();

        public async Task<IActionResult> OnGetAsync(int id)
        {
            if (User.IsInRole("Admin")) return Forbid();
            var connection = _context.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open) await connection.OpenAsync();

            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = @"SELECT e.Id, e.FullName, d.Name 
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
                        Intern = new EmployeeDetailsDto
                        {
                            Id = reader.GetInt32(0),
                            FullName = reader.IsDBNull(1) ? "Unknown" : reader.GetString(1),
                            Department = reader.IsDBNull(2) ? "N/A" : reader.GetString(2)
                        };
                        Evaluation.EmployeeId = id;
                    }
                    else
                    {
                        return RedirectToPage("./Dashboard");
                    }
                }
            }

            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "SELECT EvaluationMonth FROM InternEvaluations WHERE EmployeeId = @id";
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
                cmd.CommandText = "SELECT COUNT(*) FROM InternEvaluations WHERE EmployeeId = @checkEmpId AND EvaluationMonth = @checkMonth";
                AddParam(cmd, "@checkEmpId", Evaluation.EmployeeId);
                AddParam(cmd, "@checkMonth", Evaluation.EvaluationMonth);

                var existingCount = Convert.ToInt32(await cmd.ExecuteScalarAsync());

                if (existingCount > 0)
                {
                    ModelState.AddModelError("", $"Month {Evaluation.EvaluationMonth} has already been evaluated.");
                    await OnGetAsync(Evaluation.EmployeeId);
                    return Page();
                }

                cmd.Parameters.Clear(); 
                cmd.CommandText = @"INSERT INTO InternEvaluations 
                    (EmployeeId, EvaluatedBy, EvaluationMonth, TechnicalSkillsScore, CommunicationScore, TeamworkScore, Comments, EvaluationDate) 
                    VALUES (@empId, @evalBy, @month, @tech, @comm, @team, @feedback, @date)";

                AddParam(cmd, "@empId", Evaluation.EmployeeId);
                AddParam(cmd, "@evalBy", evalById); 
                AddParam(cmd, "@month", Evaluation.EvaluationMonth); 
                AddParam(cmd, "@tech", Evaluation.TechnicalSkillsScore);
                AddParam(cmd, "@comm", Evaluation.CommunicationScore);
                AddParam(cmd, "@team", Evaluation.TeamworkScore);
                AddParam(cmd, "@feedback", Evaluation.Comments ?? "");
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

    public class InternEvaluationInput
    {
        public int EmployeeId { get; set; }
        public int EvaluationMonth { get; set; } 
        public int TechnicalSkillsScore { get; set; }
        public int CommunicationScore { get; set; }
        public int TeamworkScore { get; set; }
        public string? Comments { get; set; }
    }
}
