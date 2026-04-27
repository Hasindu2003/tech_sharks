using HRMS.Domain.Entities.Welfare;
using HRMS.Infrastructure.Persistence;
using HRMS.UI.Pages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HRMS.UI.Pages.Welfare
{
    [Authorize(Roles = "Employee")]
    public class RequestListModel : BasePageModel
    {
        public RequestListModel(ApplicationDbContext context)
            : base(context) { }

        public List<WelfareRequest> Requests { get; set; } = new();

        public async Task OnGetAsync()
        {
            await LoadCurrentUserAsync();

            var userEmail = User.Identity?.Name;

            var employee = await _db.Employees
                .FirstOrDefaultAsync(e => e.Email == userEmail);

            if (employee == null)
            {
                Requests = new List<WelfareRequest>();
                return;
            }

            Requests = await _db.WelfareRequests
                .Include(r => r.WelfareType)
                .Include(r => r.Employee)
                .Where(r => r.EmployeeId == employee.Id)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();
        }
    }
}
