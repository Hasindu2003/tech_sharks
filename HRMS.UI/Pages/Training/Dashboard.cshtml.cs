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
        public List<ScheduledSessionDto> PastSessions { get; set; } = new(); // New list for past trainings
        public List<ProbationDetailDto> ProbationEmployees { get; set; } = new();
        public List<InternDetailDto> InternEmployees { get; set; } = new();

        public async Task OnGetAsync()
        {
            var connection = _context.Database.GetDbConnection();
            using (var cmd = connection.CreateCommand())
            {
                if (connection.State != ConnectionState.Open) 
                    await connection.OpenAsync();

                // 1. Get counts for the stats cards
                cmd.CommandText = "SELECT COUNT(*) FROM TrainingProgramRequests WHERE Status = 'Pending'";
                PendingTrainingRequestsCount = Convert.ToInt32(await cmd.ExecuteScalarAsync());

                cmd.CommandText = "SELECT COUNT(*) FROM trainings WHERE Status = 'Scheduled' AND Date >= CURRENT_DATE";
                ScheduledSessionsCount = Convert.ToInt32(await cmd.ExecuteScalarAsync());

                // 2. Fetch All Sessions (Logic to separate Upcoming vs Past)
                // Updated to include Trainer and Location
                cmd.CommandText = @"SELECT Title, Date, Trainer, Location 
                                   FROM trainings 
                                   ORDER BY Date ASC";
                
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    ScheduledSessions.Clear();
                    PastSessions.Clear();
                    DateTime today = DateTime.Today;

                    while (await reader.ReadAsync())
                    {
                        var sessionDate = reader["Date"] != DBNull.Value ? Convert.ToDateTime(reader["Date"]) : DateTime.Now;
                        var dto = new ScheduledSessionDto {
                            Title = reader["Title"]?.ToString() ?? "Untitled",
                            Date = sessionDate,
                            Trainer = reader["Trainer"]?.ToString() ?? "N/A",
                            Location = reader["Location"]?.ToString() ?? "N/A"
                        };

                        // Logic: If date is today or in the future, it is Upcoming. Otherwise, it is Past.
                        if (sessionDate.Date >= today)
                            ScheduledSessions.Add(dto);
                        else
                            PastSessions.Add(dto);
                    }
                }

                // 3. Fetch Probation Employees
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

                // 4. Fetch Interns
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
                            EvaluationsCount = evalCount,
                            Status = evalCount >= 6 ? "Completed" : "In Training"
                        });
                    }
                }
            }
        }
    }

    public class ScheduledSessionDto {
        public string Title { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public string Trainer { get; set; } = string.Empty; // Added Trainer
        public string Location { get; set; } = string.Empty; // Added Location
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
        public int EvaluationsCount { get; set; } 
    }
}