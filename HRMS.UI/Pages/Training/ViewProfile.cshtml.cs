using Microsoft.AspNetCore.Authorization;
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
    public class EvaluationRecordDto
    {
        public int Month { get; set; }
        public string EvaluatorName { get; set; } = string.Empty;
        public int Score1 { get; set; }
        public int Score2 { get; set; }
        public int Score3 { get; set; }
        public int AverageScore { get; set; }
        public string Comments { get; set; } = string.Empty;
        public DateTime EvaluationDate { get; set; }
    }

    public class EmployeeProfileDto
    {
        public int Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string EPFNumber { get; set; } = string.Empty;
        public string BranchName { get; set; } = string.Empty;
        public string DepartmentName { get; set; } = string.Empty;
        public string DesignationName { get; set; } = string.Empty;
        public string EmployeeType { get; set; } = string.Empty;
        public DateTime DateJoined { get; set; }
        public int TotalMonths { get; set; } = 6;
        public int CompletedMonths { get; set; } = 0;
        public double ProgressPercentage { get; set; }
        public double OverallAverageScore { get; set; }
        public double Score1Average { get; set; }
        public double Score2Average { get; set; }
        public double Score3Average { get; set; }
    }

    [Authorize(Roles = "Department Head, Branch Manager, Area Manager, HR Manager, HR Officer")]
    public class ViewProfileModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public ViewProfileModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public EmployeeProfileDto Profile { get; set; } = new();
        public List<EvaluationRecordDto> Evaluations { get; set; } = new();
        public List<int> MonthlyScores { get; set; } = new();
        public List<string> MonthLabels { get; set; } = new();
        public bool IsIntern { get; set; }

        public string Metric1Name => IsIntern ? "Technical Skills" : "Job Performance";
        public string Metric2Name => IsIntern ? "Communication" : "Attendance & Punctuality";
        public string Metric3Name => IsIntern ? "Teamwork & Initiative" : "Conduct & Discipline";
        public string BackUrl => IsIntern ? "/Training/InternTracking" : "/Training/ProbationTracking";
        public string EvaluateUrl => IsIntern ? $"/Training/EvaluateIntern?id={Profile.Id}" : $"/Training/EvaluateProbation?id={Profile.Id}";

        public async Task<IActionResult> OnGetAsync(int id)
        {
            if (User.IsInRole("Admin")) return Forbid();

            var connection = _context.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open) 
                await connection.OpenAsync();

            int probMonths = 6;
            int internMonths = 6;

            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = @"SELECT e.Id, e.FullName, e.EPFNumber, e.DateJoined, e.EmployeeType, 
                                           e.ProbationPeriodMonths, e.InternPeriodMonths,
                                           b.Name as BranchName, d.Name as DepartmentName, des.Title as DesignationName
                                    FROM Employees e
                                    LEFT JOIN Branches b ON e.BranchId = b.Id
                                    LEFT JOIN Departments d ON e.DepartmentId = d.Id
                                    LEFT JOIN Designations des ON e.DesignationId = des.Id
                                    WHERE e.Id = @id";
                
                var pId = cmd.CreateParameter();
                pId.ParameterName = "@id";
                pId.Value = id;
                cmd.Parameters.Add(pId);

                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        string rawType = reader["EmployeeType"]?.ToString()?.Trim() ?? "";
                        IsIntern = rawType == "0" || rawType.Contains("Intern", StringComparison.OrdinalIgnoreCase);

                        if (!reader.IsDBNull(5) && int.TryParse(reader[5].ToString(), out int pVal) && pVal > 0)
                        {
                            probMonths = pVal;
                        }

                        if (!reader.IsDBNull(6) && int.TryParse(reader[6].ToString(), out int iVal) && iVal > 0)
                        {
                            internMonths = iVal;
                        }

                        int totalMonths = IsIntern ? internMonths : probMonths;
                        if (totalMonths <= 0) totalMonths = 6;

                        Profile = new EmployeeProfileDto
                        {
                            Id = reader.GetInt32(0),
                            FullName = reader["FullName"]?.ToString() ?? "Unknown Employee",
                            EPFNumber = reader["EPFNumber"]?.ToString() ?? "N/A",
                            DateJoined = reader["DateJoined"] != DBNull.Value ? Convert.ToDateTime(reader["DateJoined"]) : DateTime.Today,
                            EmployeeType = IsIntern ? "Internship" : "Probation",
                            BranchName = reader["BranchName"]?.ToString() ?? "Branch Office",
                            DepartmentName = reader["DepartmentName"]?.ToString() ?? "General",
                            DesignationName = reader["DesignationName"]?.ToString() ?? (IsIntern ? "Trainee Intern" : "Probationary Staff"),
                            TotalMonths = totalMonths
                        };
                    }
                    else
                    {
                        return NotFound();
                    }
                }
            }

            // Fetch Evaluation History
            using (var cmd = connection.CreateCommand())
            {
                var pId = cmd.CreateParameter();
                pId.ParameterName = "@id";
                pId.Value = id;
                cmd.Parameters.Add(pId);

                if (IsIntern)
                {
                    cmd.CommandText = @"SELECT ev.EvaluationMonth, ev.TechnicalSkillsScore, ev.CommunicationScore, ev.TeamworkScore, 
                                               ev.Comments, ev.EvaluationDate, evalEmp.FullName as EvaluatorName
                                        FROM InternEvaluations ev
                                        LEFT JOIN Employees evalEmp ON ev.EvaluatedBy = evalEmp.Id
                                        WHERE ev.EmployeeId = @id
                                        ORDER BY ev.EvaluationMonth ASC";
                }
                else
                {
                    cmd.CommandText = @"SELECT ev.EvaluationMonth, ev.PerformanceScore, ev.AttendanceScore, ev.ConductScore, 
                                               ev.Comments, ev.EvaluationDate, evalEmp.FullName as EvaluatorName
                                        FROM ProbationEvaluations ev
                                        LEFT JOIN Employees evalEmp ON ev.EvaluatedBy = evalEmp.Id
                                        WHERE ev.EmployeeId = @id
                                        ORDER BY ev.EvaluationMonth ASC";
                }

                var evalList = new List<EvaluationRecordDto>();
                var scoresMap = new Dictionary<int, int>();

                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        int mNum = reader["EvaluationMonth"] != DBNull.Value ? Convert.ToInt32(reader["EvaluationMonth"]) : 0;
                        int s1 = Convert.ToInt32(reader[1]);
                        int s2 = Convert.ToInt32(reader[2]);
                        int s3 = Convert.ToInt32(reader[3]);
                        int avg = (s1 + s2 + s3) / 3;

                        scoresMap[mNum] = avg;

                        evalList.Add(new EvaluationRecordDto
                        {
                            Month = mNum,
                            Score1 = s1,
                            Score2 = s2,
                            Score3 = s3,
                            AverageScore = avg,
                            Comments = reader["Comments"]?.ToString() ?? "",
                            EvaluationDate = reader["EvaluationDate"] != DBNull.Value ? Convert.ToDateTime(reader["EvaluationDate"]) : DateTime.Now,
                            EvaluatorName = reader["EvaluatorName"]?.ToString() ?? "Supervisor / HR"
                        });
                    }
                }

                Evaluations = evalList;
                Profile.CompletedMonths = evalList.Count;
                Profile.ProgressPercentage = Math.Min(100.0, ((double)evalList.Count / Profile.TotalMonths) * 100.0);

                if (evalList.Any())
                {
                    Profile.OverallAverageScore = Math.Round(evalList.Average(e => e.AverageScore), 1);
                    Profile.Score1Average = Math.Round(evalList.Average(e => e.Score1), 1);
                    Profile.Score2Average = Math.Round(evalList.Average(e => e.Score2), 1);
                    Profile.Score3Average = Math.Round(evalList.Average(e => e.Score3), 1);
                }

                // Prepare Chart Data
                var chartData = new List<int>();
                for (int i = 1; i <= Profile.TotalMonths; i++)
                {
                    chartData.Add(scoresMap.ContainsKey(i) ? scoresMap[i] : 0);
                }
                MonthlyScores = chartData;
                MonthLabels = Enumerable.Range(1, Profile.TotalMonths).Select(m => $"Month {m}").ToList();
            }

            return Page();
        }
    }
}
