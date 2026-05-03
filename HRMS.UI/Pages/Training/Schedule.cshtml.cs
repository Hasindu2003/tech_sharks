using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using HRMS.Infrastructure.Persistence;
using System.Collections.Generic;
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
        public string TrainerName { get; set; } = string.Empty;

        [BindProperty]
        public string Location { get; set; } = string.Empty;

        [BindProperty]
        public DateTime SessionDate { get; set; } = DateTime.Now;

        [BindProperty]
        public TimeSpan StartTimeValue { get; set; }

        public List<string> ApprovedPrograms { get; set; } = new();

        public void OnGet()
        {
            ApprovedPrograms = new List<string>
            {
                "Gold Loan Appraising", "Credit Evaluation & Lending", "AML & KYC Compliance",
                "Financial Fraud Detection", "Customer Service Excellence", "Advanced Microsoft Excel",
                "Workplace Ethics & Conduct", "Strategic Leadership & Team Management"
            };
        }

        public async Task<IActionResult> OnPostAsync()
        {
            // Simple validation for date
            if (SessionDate.Date < DateTime.Now.Date)
            {
                ModelState.AddModelError("SessionDate", "Training date cannot be in the past.");
            }

            if (!ModelState.IsValid)
            {
                OnGet();
                return Page();
            }

            var connection = _context.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open) 
                await connection.OpenAsync();

            using (var cmd = connection.CreateCommand())
            {
                // Note: We no longer need EmployeeId or RequestId here 
                // because we are scheduling a general session.
                cmd.CommandText = @"INSERT INTO trainings (Title, Date, StartTime, Trainer, Location, Status) 
                                   VALUES (@title, @date, @sTime, @trainer, @loc, 'Scheduled')";
                
                cmd.Parameters.Add(CreateParam(cmd, "@title", SelectedProgramTitle));
                cmd.Parameters.Add(CreateParam(cmd, "@date", SessionDate.Date));
                cmd.Parameters.Add(CreateParam(cmd, "@sTime", StartTimeValue));
                cmd.Parameters.Add(CreateParam(cmd, "@trainer", TrainerName));
                cmd.Parameters.Add(CreateParam(cmd, "@loc", Location));

                await cmd.ExecuteNonQueryAsync();
            }

            return RedirectToPage("./Dashboard");
        }

        private IDbDataParameter CreateParam(IDbCommand cmd, string name, object? value)
        {
            var param = cmd.CreateParameter();
            param.ParameterName = name;
            param.Value = value ?? DBNull.Value;
            return param;
        }
    }
}