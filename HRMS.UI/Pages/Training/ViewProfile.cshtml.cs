using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using HRMS.Infrastructure.Persistence;
using System.Data;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using System;

namespace HRMS.UI.Pages.Training
{
    public class ViewProfileModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public ViewProfileModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public string FullName { get; set; } = string.Empty;
        public string EmpType { get; set; } = string.Empty;
        public int EmpId { get; set; }
        public List<int> MonthlyScores { get; set; } = new();

        public async Task<IActionResult> OnGetAsync(int id)
        {
            EmpId = id;
            var connection = _context.Database.GetDbConnection();
            
            if (connection.State != ConnectionState.Open) 
                await connection.OpenAsync();

            using (var cmd = connection.CreateCommand())
            {
                
                cmd.CommandText = "SELECT FirstName, LastName, EmployeeType FROM Employees WHERE Id = @id";
                var pId = cmd.CreateParameter();
                pId.ParameterName = "@id";
                pId.Value = id;
                cmd.Parameters.Add(pId);

                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        FullName = $"{reader["FirstName"]} {reader["LastName"]}";
                        string rawType = reader["EmployeeType"]?.ToString()?.Trim() ?? "";

                        if (rawType == "0" || rawType.Contains("Intern", StringComparison.OrdinalIgnoreCase)) 
                        {
                            EmpType = "Intern";
                        }
                        else 
                        {
                            EmpType = "Probation";
                        }
                    }
                    else return NotFound();
                }

              
                cmd.Parameters.Clear();
                var pId2 = cmd.CreateParameter();
                pId2.ParameterName = "@id";
                pId2.Value = id;
                cmd.Parameters.Add(pId2);

                string query = "";
                if (EmpType == "Intern")
                {
                    query = "SELECT EvaluationMonth, TechnicalSkillsScore, CommunicationScore, TeamworkScore FROM InternEvaluations WHERE EmployeeId = @id";
                }
                else
                {
                    query = "SELECT EvaluationMonth, PerformanceScore, AttendanceScore, ConductScore FROM ProbationEvaluations WHERE EmployeeId = @id";
                }

                cmd.CommandText = query;
                var scoresArray = new int[6] { 0, 0, 0, 0, 0, 0 };

                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        var monthRaw = reader["EvaluationMonth"];
                        int avg = 0;

                        if (EmpType == "Intern")
                        {
                            int s1 = reader["TechnicalSkillsScore"] != DBNull.Value ? Convert.ToInt32(reader["TechnicalSkillsScore"]) : 0;
                            int s2 = reader["CommunicationScore"] != DBNull.Value ? Convert.ToInt32(reader["CommunicationScore"]) : 0;
                            int s3 = reader["TeamworkScore"] != DBNull.Value ? Convert.ToInt32(reader["TeamworkScore"]) : 0;
                            avg = (s1 + s2 + s3) / 3;
                        }
                        else
                        {
                            int s1 = reader["PerformanceScore"] != DBNull.Value ? Convert.ToInt32(reader["PerformanceScore"]) : 0;
                            int s2 = reader["AttendanceScore"] != DBNull.Value ? Convert.ToInt32(reader["AttendanceScore"]) : 0;
                            int s3 = reader["ConductScore"] != DBNull.Value ? Convert.ToInt32(reader["ConductScore"]) : 0;
                            avg = (s1 + s2 + s3) / 3;
                        }

                        int monthNum = 0;
                        if (monthRaw != null)
                        {
                            string monthStr = monthRaw.ToString() ?? "";
                            string digits = new string(monthStr.Where(char.IsDigit).ToArray());
                            if (!string.IsNullOrEmpty(digits)) int.TryParse(digits, out monthNum);
                        }

                        if (monthNum >= 1 && monthNum <= 6) 
                        {
                            scoresArray[monthNum - 1] = avg;
                        }
                    }
                }
                MonthlyScores = scoresArray.ToList();
            }

            return Page();
        }
    }
}