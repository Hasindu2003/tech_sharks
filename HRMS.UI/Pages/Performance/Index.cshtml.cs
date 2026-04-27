using HRMS.Domain.Entities.Core;
using HRMS.Infrastructure.Persistence;
using HRMS.UI.Pages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace HRMS.UI.Pages.Performance
{
    [Authorize(Roles = "Admin,SeniorManagement")]
    public class IndexModel : BasePageModel
    {
        public IndexModel(ApplicationDbContext context) : base(context) { }

        public int TotalEmployees { get; set; }
        public double AvgPerformanceScore { get; set; }
        public int TopPerformerCount { get; set; }
        public double GoalsAchievedPercent { get; set; }
        public int PendingReviews { get; set; }

        public List<DepartmentPerformance> DepartmentStats { get; set; } = new();
        public List<EmployeePerformance> AllEmployees { get; set; } = new();

        public async Task OnGetAsync()
        {
            await LoadCurrentUserAsync();

            var allEmployees = await _db.Employees
                .Include(e => e.Department)
                .Include(e => e.Designation)
                .ToListAsync();

            TotalEmployees = allEmployees.Count;

            var cutoff = DateTime.Today.AddDays(-90);
            var today = DateTime.Today;
            var year = today.Year;

            // ── Bulk load all data ────────────────────────────────────────────
            var allAttendance = await _db.Attendances
                .Where(a => a.Date >= cutoff && a.Date <= today)
                .ToListAsync();

            var allEntitlements = await _db.LeaveEntitlements
                .Where(e => e.Year == year)
                .ToListAsync();

            var allWelfareRequests = await _db.WelfareRequests
                .Where(r => !r.IsDraft)
                .ToListAsync();

            // Training participation & scores
            var allEmpTrainings = await _db.EmployeeTrainings
                .ToListAsync();

            // Employee's own feedback/rating of trainings they attended
            var allTrainingFeedback = await _db.TrainingFeedbacks
                .ToListAsync();

            // Supervisor ratings about intern employees
            var allInternFeedback = await _db.InternFeedbacks
                .Include(f => f.InternProgram)
                .ToListAsync();

            // Supervisor ratings about employees on probation
            var allProbationFeedback = await _db.ProbationFeedbacks
                .Include(f => f.ProbationPeriod)
                .ToListAsync();

            PendingReviews = allWelfareRequests.Count(r => r.CurrentStatus == "Pending");

            var scores = new List<EmployeePerformance>();

            foreach (var emp in allEmployees)
            {
                // ── 1. ATTENDANCE SCORE (30%) ─────────────────────────────────
                var empAtt = allAttendance.Where(a => a.EmployeeId == emp.Id).ToList();
                double attendanceScore = 50;
                double punctualityScore = 50;
                int presentDays = 0;
                int totalDays = 0;
                int lateDays = 0;

                if (empAtt.Any())
                {
                    totalDays = empAtt.Count;
                    presentDays = empAtt.Count(a =>
                        string.Equals(a.Status, "Present", StringComparison.OrdinalIgnoreCase));

                    attendanceScore = totalDays > 0
                        ? Math.Round((double)presentDays / totalDays * 100, 1)
                        : 50;

                    // ── 2. PUNCTUALITY SCORE (15%) ────────────────────────────
                    var presentWithTime = empAtt
                        .Where(a => string.Equals(a.Status, "Present",
                                        StringComparison.OrdinalIgnoreCase)
                                 && a.TimeIn.HasValue)
                        .ToList();

                    if (presentWithTime.Any())
                    {
                        int onTime = presentWithTime.Count(a =>
                            a.TimeIn!.Value.TimeOfDay <= new TimeSpan(8, 0, 0));
                        lateDays = presentWithTime.Count - onTime;
                        punctualityScore = Math.Round(
                            (double)onTime / presentWithTime.Count * 100, 1);
                    }
                }

                // ── 3. LEAVE SCORE (20%) ──────────────────────────────────────
                var empEntitlements = allEntitlements
                    .Where(e => e.EmployeeId == emp.Id).ToList();

                double leaveScore = 70;
                int leaveDaysUsed = 0;
                int leaveDaysTotal = 0;

                if (empEntitlements.Any())
                {
                    leaveDaysTotal = empEntitlements.Sum(e => e.TotalDays);
                    leaveDaysUsed = empEntitlements.Sum(e => e.UsedDays);
                    leaveScore = leaveDaysTotal > 0
                        ? Math.Round((1.0 - (double)leaveDaysUsed / leaveDaysTotal) * 100, 1)
                        : 70;
                    leaveScore = Math.Max(0, Math.Min(100, leaveScore));
                }

                // ── 4. TRAINING SCORE (25%) ───────────────────────────────────
                //
                // Component A — Participation (from EmployeeTraining)
                //   = attended% weighted with numeric score if available
                //
                // Component B — Engagement (from TrainingFeedback)
                //   = employee's own training ratings converted to 0-100
                //   (high rating = more engaged/motivated learner)
                //
                // Component C — Supervisor Assessment (from InternFeedback + ProbationFeedback)
                //   = supervisor's rating of the employee's performance
                //
                // Final = A*0.45 + B*0.25 + C*0.30  (when all available)

                var empTrainings = allEmpTrainings.Where(et => et.EmployeeId == emp.Id).ToList();
                var empTrnFeedback = allTrainingFeedback.Where(f => f.EmployeeId == emp.Id).ToList();
                var empInternFeedback = allInternFeedback.Where(f => f.InternProgram.EmployeeId == emp.Id).ToList();
                var empProbFeedback = allProbationFeedback.Where(f => f.ProbationPeriod.EmployeeId == emp.Id).ToList();

                double trainingScore = 50;
                int trainingsAttended = 0;
                int trainingsTotal = 0;
                int feedbackCount = 0;
                double avgFeedbackRating = 0;

                // Component A: Participation
                bool hasParticipation = empTrainings.Any();
                double compA = 50;
                if (hasParticipation)
                {
                    trainingsTotal = empTrainings.Count;
                    trainingsAttended = empTrainings.Count(et =>
                        string.Equals(et.AttendanceStatus, "Attended",
                            StringComparison.OrdinalIgnoreCase));

                    double partRate = Math.Round(
                        (double)trainingsAttended / trainingsTotal * 100, 1);

                    var numericScores = empTrainings
                        .Where(et => !string.IsNullOrEmpty(et.Score)
                                  && double.TryParse(et.Score, out _))
                        .Select(et => double.Parse(et.Score!))
                        .ToList();

                    double scoreComp = numericScores.Any()
                        ? Math.Round(numericScores.Average(), 1)
                        : partRate;

                    compA = Math.Round((partRate * 0.6) + (scoreComp * 0.4), 1);
                }

                // Component B: Employee engagement (their own training ratings)
                bool hasEngagement = empTrnFeedback.Any();
                double compB = 50;
                if (hasEngagement)
                {
                    compB = Math.Round(empTrnFeedback.Average(f => f.Rating) / 5.0 * 100, 1);
                }

                // Component C: Supervisor assessment
                var supervisorRatings = new List<int>();
                supervisorRatings.AddRange(empInternFeedback.Select(f => f.Rating));
                supervisorRatings.AddRange(empProbFeedback.Select(f => f.Rating));

                bool hasSupervisor = supervisorRatings.Any();
                double compC = 50;
                if (hasSupervisor)
                {
                    avgFeedbackRating = Math.Round(supervisorRatings.Average(), 1);
                    feedbackCount = supervisorRatings.Count;
                    compC = Math.Round(avgFeedbackRating / 5.0 * 100, 1);
                }

                // Blend components based on what's available
                int available = (hasParticipation ? 1 : 0)
                              + (hasEngagement ? 1 : 0)
                              + (hasSupervisor ? 1 : 0);

                trainingScore = available switch
                {
                    3 => Math.Round((compA * 0.45) + (compB * 0.25) + (compC * 0.30), 1),
                    2 when hasParticipation && hasSupervisor
                        => Math.Round((compA * 0.60) + (compC * 0.40), 1),
                    2 when hasParticipation && hasEngagement
                        => Math.Round((compA * 0.70) + (compB * 0.30), 1),
                    2 when hasEngagement && hasSupervisor
                        => Math.Round((compB * 0.40) + (compC * 0.60), 1),
                    1 when hasParticipation => compA,
                    1 when hasEngagement => compB,
                    1 when hasSupervisor => compC,
                    _ => 50  // no data — neutral
                };

                trainingScore = Math.Max(0, Math.Min(100, trainingScore));

                // ── 5. WELFARE / DISCIPLINE (10%) ─────────────────────────────
                var empWelfare = allWelfareRequests.Where(r => r.EmployeeId == emp.Id).ToList();
                double welfareScore = 50;

                if (empWelfare.Any())
                {
                    int rejected = empWelfare.Count(r => r.Status == "Rejected");
                    welfareScore = Math.Round(
                        (1.0 - (double)rejected / empWelfare.Count) * 100, 1);
                }

                // ── FINAL WEIGHTED SCORE ──────────────────────────────────────
                double finalScore = Math.Max(0, Math.Min(100,
                    (attendanceScore * 0.30) +
                    (punctualityScore * 0.15) +
                    (leaveScore * 0.20) +
                    (trainingScore * 0.25) +
                    (welfareScore * 0.10)));

                finalScore = Math.Round(finalScore, 1);

                string grade = finalScore switch
                {
                    >= 90 => "A+",
                    >= 80 => "A",
                    >= 70 => "B",
                    >= 60 => "C",
                    _ => "D"
                };

                scores.Add(new EmployeePerformance
                {
                    Employee = emp,
                    AttendanceScore = attendanceScore,
                    PunctualityScore = punctualityScore,
                    LeaveScore = leaveScore,
                    TrainingScore = trainingScore,
                    WelfareScore = welfareScore,
                    PerformanceScore = finalScore,
                    Grade = grade,
                    PresentDays = presentDays,
                    TotalDays = totalDays,
                    LateDays = lateDays,
                    LeaveDaysUsed = leaveDaysUsed,
                    LeaveDaysTotal = leaveDaysTotal,
                    TrainingsAttended = trainingsAttended,
                    TrainingsTotal = trainingsTotal,
                    FeedbackCount = feedbackCount,
                    AvgFeedbackRating = avgFeedbackRating,
                    WelfareTotal = empWelfare.Count,
                    WelfareApproved = empWelfare.Count(r =>
                        r.Status == "Approved" ||
                        r.Status == "PaymentCompleted" ||
                        r.Status == "UnderReview"),
                    WelfareRejected = empWelfare.Count(r => r.Status == "Rejected"),
                    WelfareCompleted = empWelfare.Count(r => r.Status == "PaymentCompleted"),
                    Status = emp.Status
                });
            }

            scores = scores.OrderByDescending(e => e.PerformanceScore).ToList();
            for (int i = 0; i < scores.Count; i++) scores[i].Rank = i + 1;

            AllEmployees = scores;
            TopPerformerCount = scores.Count(e => e.PerformanceScore >= 80);

            AvgPerformanceScore = scores.Any()
                ? Math.Round(scores.Average(e => e.PerformanceScore), 1) : 0;

            int totalNonDraft = allWelfareRequests.Count;
            int totalCompleted = allWelfareRequests.Count(r => r.Status == "PaymentCompleted");
            GoalsAchievedPercent = totalNonDraft > 0
                ? Math.Round((double)totalCompleted / totalNonDraft * 100, 1) : 0;

            var deptGroups = scores
                .Where(e => e.Employee.Department != null)
                .GroupBy(e => e.Employee.Department!.Name)
                .ToList();

            double maxScore = deptGroups.Any()
                ? deptGroups.Max(g => g.Average(e => e.PerformanceScore)) : 100;

            DepartmentStats = deptGroups.Select(g => new DepartmentPerformance
            {
                DepartmentName = g.Key,
                AvgScore = Math.Round(g.Average(e => e.PerformanceScore), 1),
                EmployeeCount = g.Count(),
                BarHeightPercent = maxScore > 0
                    ? (int)Math.Round(g.Average(e => e.PerformanceScore) / maxScore * 100)
                    : 0
            }).OrderByDescending(d => d.AvgScore).Take(6).ToList();
        }
    }

    public class EmployeePerformance
    {
        public Employee Employee { get; set; } = null!;
        public int Rank { get; set; }
        public double AttendanceScore { get; set; }
        public double PunctualityScore { get; set; }
        public double LeaveScore { get; set; }
        public double TrainingScore { get; set; }
        public double WelfareScore { get; set; }
        public double PerformanceScore { get; set; }
        public string Grade { get; set; } = "C";
        public string Status { get; set; } = "Active";
        public int PresentDays { get; set; }
        public int TotalDays { get; set; }
        public int LateDays { get; set; }
        public int LeaveDaysUsed { get; set; }
        public int LeaveDaysTotal { get; set; }
        public int TrainingsAttended { get; set; }
        public int TrainingsTotal { get; set; }
        public int FeedbackCount { get; set; }
        public double AvgFeedbackRating { get; set; }
        public int WelfareTotal { get; set; }
        public int WelfareApproved { get; set; }
        public int WelfareRejected { get; set; }
        public int WelfareCompleted { get; set; }

        public string GradeBadgeColor => Grade switch
        {
            "A+" => "bg-emerald-100 text-emerald-700",
            "A" => "bg-green-100 text-green-700",
            "B" => "bg-blue-100 text-blue-700",
            "C" => "bg-yellow-100 text-yellow-700",
            _ => "bg-red-100 text-red-700"
        };

        public string RankBadgeColor => Rank switch
        {
            1 => "bg-yellow-100 text-yellow-700",
            2 => "bg-gray-100 text-gray-500",
            3 => "bg-orange-100 text-orange-600",
            _ => "bg-gray-50 text-gray-400"
        };
    }

    public class DepartmentPerformance
    {
        public string DepartmentName { get; set; } = "";
        public double AvgScore { get; set; }
        public int EmployeeCount { get; set; }
        public int BarHeightPercent { get; set; }
    }
}
