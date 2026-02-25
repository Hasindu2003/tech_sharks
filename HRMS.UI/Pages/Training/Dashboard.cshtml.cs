using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using HRMS.Infrastructure.Persistence;
using System.Threading.Tasks;
using System.Linq;
using System;
using System.Collections.Generic;
using System.Data;

namespace HRMS.UI.Pages.Training
{
    public class DashboardModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public DashboardModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public int PendingTrainingRequestsCount { get; set; }
        public int ScheduledSessionsCount { get; set; } 
        public List<ScheduledSessionDto> ScheduledSessions { get; set; } = new();
        public List<ProbationDetailDto> ProbationEmployees { get; set; } = new();
        public List<InternDetailDto> InternEmployees { get; set; } = new();

        public async Task OnGetAsync()
        {
            var connection = _context.Database.GetDbConnection();
            using (var cmd = connection.CreateCommand())
            {
                if (connection.State != ConnectionState.Open) 
                    await connection.OpenAsync();

                // 1. Pending Requests Count
                cmd.CommandText = "SELECT COUNT(*) FROM TrainingProgramRequests WHERE Status = 'Pending'";
                PendingTrainingRequestsCount = Convert.ToInt32(await cmd.ExecuteScalarAsync());

                // 2. Scheduled Sessions Count
                cmd.CommandText = "SELECT COUNT(*) FROM trainings WHERE Status = 'Scheduled'";
                ScheduledSessionsCount = Convert.ToInt32(await cmd.ExecuteScalarAsync());

                // 3. Fetch Scheduled Sessions
                cmd.CommandText = @"SELECT t.Title, t.Date, CONCAT(e.FirstName, ' ', e.LastName) as EmpName 
                                   FROM trainings t 
                                   JOIN Employees e ON t.EmployeeId = e.Id 
                                   WHERE t.Status = 'Scheduled' 
                                   ORDER BY t.Date DESC LIMIT 5";
                
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    ScheduledSessions.Clear();
                    while (await reader.ReadAsync())
                    {
                        ScheduledSessions.Add(new ScheduledSessionDto {
                            Title = reader["Title"]?.ToString() ?? "Untitled",
                            Date = reader["Date"] != DBNull.Value ? Convert.ToDateTime(reader["Date"]) : DateTime.Now,
                            EmployeeName = reader["EmpName"]?.ToString() ?? "Unknown"
                        });
                    }
                }

                // 4. Fetch Probation Employees with Progress
                cmd.CommandText = @"SELECT e.Id, e.FirstName, e.LastName, e.DateJoined,
                                   (SELECT MAX(EvaluationMonth) FROM ProbationEvaluations WHERE EmployeeId = e.Id) as LastMonth
                                   FROM Employees e 
                                   WHERE e.EmployeeType = 'Probation' OR e.Status = 'Probation'";
                
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    ProbationEmployees.Clear();
                    while (await reader.ReadAsync())
                    {
                        int month = reader["LastMonth"] != DBNull.Value ? Convert.ToInt32(reader["LastMonth"]) : 0;
                        ProbationEmployees.Add(new ProbationDetailDto {
                            Id = Convert.ToInt32(reader["Id"]),
                            FirstName = reader["FirstName"]?.ToString() ?? string.Empty,
                            LastName = reader["LastName"]?.ToString() ?? string.Empty,
                            DateJoined = reader["DateJoined"] != DBNull.Value ? Convert.ToDateTime(reader["DateJoined"]) : DateTime.Now,
                            CurrentMonth = month,
                            ProgressPercentage = (month / 6.0) * 100,
                            Status = month >= 6 ? "Review Pending" : "On Track"
                        });
                    }
                }

                // 5. Fetch Interns (UPDATED)
                // මෙතනදී අපි 'COUNT' එක SQL එකෙන්ම අරගෙන එනවා
                cmd.CommandText = @"SELECT e.Id, e.FirstName, e.LastName,
                                   (SELECT COUNT(*) FROM InternEvaluations WHERE EmployeeId = e.Id) as EvalCount
                                   FROM Employees e WHERE e.EmployeeType = 'Intern'";
                
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    InternEmployees.Clear();
                    while (await reader.ReadAsync())
                    {
                        int evalCount = reader["EvalCount"] != DBNull.Value ? Convert.ToInt32(reader["EvalCount"]) : 0;
                        
                        InternEmployees.Add(new InternDetailDto {
                            Id = Convert.ToInt32(reader["Id"]),
                            FirstName = reader["FirstName"]?.ToString() ?? string.Empty,
                            LastName = reader["LastName"]?.ToString() ?? string.Empty,
                            EvaluationsCount = evalCount, // අලුත් Property එකට අගය ලබා දීම
                            Status = evalCount >= 6 ? "Completed" : "In Training"
                        });
                    }
                }
            }
        }
    }

    // --- DTOs ---

    public class ScheduledSessionDto {
        public string Title { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public string EmployeeName { get; set; } = string.Empty;
    }

    public class ProbationDetailDto {
        public int Id { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public DateTime DateJoined { get; set; }
        public int CurrentMonth { get; set; }
        public double ProgressPercentage { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    public class InternDetailDto {
        public int Id { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        // වැදගත්ම කොටස: මේ Property එක තමයි ඔයාගේ Error එකට හේතුව වුණේ
        public int EvaluationsCount { get; set; } 
    }
}