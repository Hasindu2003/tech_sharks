using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using HRMS.Infrastructure.Persistence;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System;
using System.Data;

namespace HRMS.UI.Pages.Training
{
    public class ScheduleModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public ScheduleModel(ApplicationDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public string SelectedProgramTitle { get; set; } = string.Empty;

        [BindProperty]
        public List<int>? SelectedRequestIds { get; set; } // Nullable list

        [BindProperty]
        public DateTime SessionDate { get; set; } = DateTime.Now;

        [BindProperty]
        public TimeSpan StartTimeValue { get; set; }

        public List<string> ApprovedPrograms { get; set; } = new List<string>();
        public List<ApprovedEmployeeDto> ApprovedEmployees { get; set; } = new();

        public async Task OnGetAsync()
        {
            ApprovedPrograms = new List<string>
            {
                "Gold Loan Appraising", "Credit Evaluation & Lending", "Leasing & Hire Purchase Operations",
                "Debt Recovery & Negotiation Skills", "AML & KYC Compliance", "Financial Fraud Detection",
                "Customer Service Excellence", "Financial Product Sales & Marketing", "Core Banking System (CBS) Training",
                "Cybersecurity & Data Privacy", "Advanced Microsoft Excel", "Microfinance Field Best Practices",
                "Workplace Ethics & Conduct", "Strategic Leadership & Team Management", "IT Infrastructure & Troubleshooting"
            };

            // LINQ query එකේදී null-safe විදිහට දත්ත ගැනීම
            ApprovedEmployees = await (from r in _context.Set<HRMS.Domain.Entities.Training.TrainingProgramRequest>()
                                     join e in _context.Employees on r.EmployeeId equals e.Id
                                     join d in _context.Departments on e.DepartmentId equals d.Id
                                     where r.Status == "Approved"
                                     select new ApprovedEmployeeDto
                                     {
                                         RequestId = r.Id,
                                         EmployeeId = e.Id,
                                         EmployeeName = (e.FirstName ?? "") + " " + (e.LastName ?? ""),
                                         DepartmentName = d.Name ?? "N/A",
                                         TrainingTitle = r.Title ?? "General Training"
                                     }).ToListAsync();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            // Past Date Validation
            if (SessionDate.Date < DateTime.Now.Date)
            {
                ModelState.AddModelError("SessionDate", "Training date cannot be in the past.");
            }

            // List එක null ද නැද්ද කියලා check කිරීම (Warning Fix)
            if (SelectedRequestIds == null || !SelectedRequestIds.Any())
            {
                ModelState.AddModelError("", "Please select at least one employee.");
            }

            if (!ModelState.IsValid)
            {
                await OnGetAsync();
                return Page();
            }

            var connection = _context.Database.GetDbConnection();
            
            // Connection එක null ද කියා පරීක්ෂා කිරීම (Warning Fix)
            if (connection == null) return Page();

            if (connection.State != ConnectionState.Open) 
                await connection.OpenAsync();

            // SelectedRequestIds null නොවන බව සහතික නිසා පාවිච්චි කළ හැක
            foreach (var requestId in SelectedRequestIds!) 
            {
                var request = await _context.Set<HRMS.Domain.Entities.Training.TrainingProgramRequest>().FindAsync(requestId);
                
                // Request එක null නොවන බව සහතික කිරීම (Warning Fix)
                if (request != null)
                {
                    using (var cmd = connection.CreateCommand())
                    {
                        // 1. Insert Training
                        cmd.CommandText = "INSERT INTO trainings (Title, Date, StartTime, EmployeeId, Status) VALUES (@title, @date, @sTime, @empId, 'Scheduled')";
                        
                        cmd.Parameters.Add(CreateParam(cmd, "@title", SelectedProgramTitle ?? "Untitled"));
                        cmd.Parameters.Add(CreateParam(cmd, "@date", SessionDate.Date));
                        cmd.Parameters.Add(CreateParam(cmd, "@sTime", StartTimeValue));
                        cmd.Parameters.Add(CreateParam(cmd, "@empId", request.EmployeeId)); 
                        await cmd.ExecuteNonQueryAsync();

                        // 2. Update Status
                        cmd.Parameters.Clear();
                        cmd.CommandText = "UPDATE TrainingProgramRequests SET Status = 'Scheduled' WHERE Id = @rId";
                        cmd.Parameters.Add(CreateParam(cmd, "@rId", requestId));
                        await cmd.ExecuteNonQueryAsync();

                        // 3. Insert Notification
                        cmd.Parameters.Clear();
                        cmd.CommandText = "INSERT INTO Notifications (EmployeeId, Message, IsRead, CreatedAt) VALUES (@eId, @msg, 0, NOW())";
                        cmd.Parameters.Add(CreateParam(cmd, "@eId", request.EmployeeId));
                        cmd.Parameters.Add(CreateParam(cmd, "@msg", $"Training Scheduled: {SelectedProgramTitle} on {SessionDate:yyyy-MM-dd}"));
                        await cmd.ExecuteNonQueryAsync();
                    }
                }
            }

            return RedirectToPage("./Dashboard");
        }

        // Object එක null විය හැකි බව පෙන්වීමට 'object?' පාවිච්චි කිරීම
        private IDbDataParameter CreateParam(IDbCommand cmd, string name, object? value)
        {
            var param = cmd.CreateParameter();
            param.ParameterName = name;
            param.Value = value ?? DBNull.Value;
            return param;
        }
    }

    public class ApprovedEmployeeDto
    {
        public int RequestId { get; set; }
        public int EmployeeId { get; set; }
        public string EmployeeName { get; set; } = string.Empty;
        public string DepartmentName { get; set; } = string.Empty;
        public string TrainingTitle { get; set; } = string.Empty;
    }
}