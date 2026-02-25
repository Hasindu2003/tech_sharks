using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using HRMS.Infrastructure.Persistence;
using System.Threading.Tasks;

namespace HRMS.UI.Pages.Training
{
    public class DetailsModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public DetailsModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public dynamic? RequestDetails { get; set; }

        public async Task<IActionResult> OnGetAsync(int id)
        {
            // Join එකක් දාලා අවශ්‍ය දත්ත ටික ගමු
            RequestDetails = await (from r in _context.TrainingProgramRequests
                                    join e in _context.Employees on r.EmployeeId equals e.Id
                                    where r.Id == id
                                    select new
                                    {
                                        r.Id,
                                        r.Title,
                                        r.Description,
                                        r.Status,
                                        r.RequestedDate,
                                        EmployeeName = e.FirstName + " " + e.LastName,
                                        e.Email
                                    }).FirstOrDefaultAsync();

            if (RequestDetails == null) return RedirectToPage("./Manage");
            return Page();
        }

        public async Task<IActionResult> OnPostUpdateStatusAsync(int id, string status)
        {
            var request = await _context.TrainingProgramRequests.FindAsync(id);
            if (request != null)
            {
                request.Status = status; // 'Approved' හෝ 'Rejected'
                await _context.SaveChangesAsync();
            }

            return RedirectToPage("./Manage");
        }
    }
}