using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using HRMS.Infrastructure.Persistence;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;

namespace HRMS.UI.Pages.Training
{
    public class ManageModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public ManageModel(ApplicationDbContext context)
        {
            _context = context;
        }

        // Error එක අයින් වෙන්න මෙතන string.Empty පාවිච්චි කරලා තියෙනවා
        public class RequestView
        {
            public int Id { get; set; }
            public string EmployeeName { get; set; } = string.Empty;
            public string ProgramName { get; set; } = string.Empty;
            public string Status { get; set; } = string.Empty;
            public string Date { get; set; } = string.Empty;
        }

        public List<RequestView> TrainingRequests { get; set; } = new();

        public async Task OnGetAsync()
        {
            // Database එකෙන් Employees සහ Training Requests ලින්ක් කරලා දත්ත ගැනීම
            var data = await (from r in _context.TrainingProgramRequests
                              join e in _context.Employees on r.EmployeeId equals e.Id
                              select new RequestView
                              {
                                  Id = r.Id,
                                  EmployeeName = e.FirstName + " " + e.LastName,
                                  ProgramName = r.Title ?? "N/A",
                                  Status = r.Status ?? "Pending",
                                  Date = r.RequestedDate.ToString("yyyy-MM-dd")
                              }).OrderByDescending(x => x.Id).ToListAsync();

            TrainingRequests = data;
        }
    }
}