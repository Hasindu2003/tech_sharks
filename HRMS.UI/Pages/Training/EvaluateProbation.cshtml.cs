using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using HRMS.Infrastructure.Persistence;
using System.Data;
using System.Data.Common;

namespace HRMS.UI.Pages.Training
{
    public class EvaluateProbationModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public EvaluateProbationModel(ApplicationDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public ProbationEvaluationInput Evaluation { get; set; } = new();
        
   
        public EmployeeDetailsDto Employee { get; set; } = new();

  
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
                        Employee = new EmployeeDetailsDto
                        {
                            Id = reader.GetInt32(0),
                            FullName = $"{reader.GetString(1)} {reader.GetString(2)}",
                            Department = reader.IsDBNull(3) ? "General" : reader.GetString(3)
                        };
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
            if (!ModelState.IsValid) return Page();

            var connection = _context.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open) await connection.OpenAsync();

            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = @"INSERT INTO ProbationEvaluations 
                    (EmployeeId, EvaluatedBy, EvaluationMonth, PerformanceScore, AttendanceScore, ConductScore, Comments, EvaluationDate) 
                    VALUES (@empId, @evalBy, @month, @perf, @att, @cond, @comm, @date)";

                AddParam(cmd, "@empId", Evaluation.EmployeeId);
                AddParam(cmd, "@evalBy", 1); 
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