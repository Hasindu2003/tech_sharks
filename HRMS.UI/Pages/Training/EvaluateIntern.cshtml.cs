using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using HRMS.Infrastructure.Persistence;
using System.Data;
using System.Data.Common;

namespace HRMS.UI.Pages.Training
{
    public class EmployeeDetailsDto
    {
        public int Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
    }

    public class EvaluateInternModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public EvaluateInternModel(ApplicationDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public InternEvaluationInput Evaluation { get; set; } = new();
        
        public EmployeeDetailsDto Intern { get; set; } = new();

      
        public List<int> ExistingMonths { get; set; } = new();

        public async Task<IActionResult> OnGetAsync(int id)
        {
            var connection = _context.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open) await connection.OpenAsync();

          
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = @"SELECT e.Id, e.FirstName, e.LastName, d.Name 
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
                            FullName = $"{reader.GetString(1)} {reader.GetString(2)}",
                            Department = reader.IsDBNull(3) ? "N/A" : reader.GetString(3)
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
            if (!ModelState.IsValid) return Page();

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
                AddParam(cmd, "@evalBy", 1); 
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