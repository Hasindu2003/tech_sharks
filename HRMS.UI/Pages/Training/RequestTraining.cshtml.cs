using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using HRMS.Infrastructure.Persistence;
using HRMS.Domain.Entities.Training;
using System.Data;

namespace HRMS.UI.Pages.Training
{
    public class RequestTrainingModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public RequestTrainingModel(ApplicationDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public TrainingProgramRequest TrainingRequest { get; set; } = new();

        public string EmployeeName { get; set; } = "";
        public string EmployeeTypeDisplayName { get; set; } = "";
        public bool IsEligible { get; set; } = false;

        public async Task<IActionResult> OnGetAsync()
        {
            int testEmployeeId = 36; 

            var connection = _context.Database.GetDbConnection();
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT FirstName, LastName, EmployeeType FROM Employees WHERE Id = @id";
                var parameter = command.CreateParameter();
                parameter.ParameterName = "@id";
                parameter.Value = testEmployeeId;
                command.Parameters.Add(parameter);

                if (connection.State != ConnectionState.Open)
                    await connection.OpenAsync();

                using (var reader = await command.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        // reader[index] null විය හැකි නිසා ආරක්ෂිතව string වලට ගමු
                        string fName = reader["FirstName"]?.ToString() ?? "Unknown";
                        string lName = reader["LastName"]?.ToString() ?? "";
                        
                        EmployeeName = $"{fName} {lName}".Trim();
                        EmployeeTypeDisplayName = reader["EmployeeType"]?.ToString() ?? "N/A";

                        if (string.Equals(EmployeeTypeDisplayName, "Permanent", StringComparison.OrdinalIgnoreCase))
                        {
                            IsEligible = true;
                        }
                    }
                }
            }
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            int testEmployeeId = 36; 
            string typeFromDb = "";

            var connection = _context.Database.GetDbConnection();
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT EmployeeType FROM Employees WHERE Id = @id";
                var parameter = command.CreateParameter();
                parameter.ParameterName = "@id";
                parameter.Value = testEmployeeId;
                command.Parameters.Add(parameter);

                if (connection.State != ConnectionState.Open)
                    await connection.OpenAsync();

                var result = await command.ExecuteScalarAsync();
                typeFromDb = result?.ToString() ?? "";
            }

            if (!string.Equals(typeFromDb, "Permanent", StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError("", "පුහුණු වැඩසටහන් ඉල්ලුම් කළ හැක්කේ ස්ථිර සේවකයන්ට පමණි.");
                return Page();
            }

            try 
            {
                // TrainingRequest object එක null නොවන බව තහවුරු කිරීමට model validation පාවිච්චි කරමු
                var newRequest = new TrainingProgramRequest
                {
                    EmployeeId = testEmployeeId,
                    Title = TrainingRequest.Title ?? "Not Specified",
                    Description = TrainingRequest.Description ?? "",
                    RequestedDate = DateTime.Now,
                    Status = "Pending"
                };

                _context.TrainingProgramRequests.Add(newRequest);
                await _context.SaveChangesAsync();

                return RedirectToPage("./Dashboard");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Error: " + ex.Message);
                return Page();
            }
        }
    }
}