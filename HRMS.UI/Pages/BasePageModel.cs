using HRMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HRMS.UI.Pages
{
    public abstract class BasePageModel : PageModel
    {
        protected readonly ApplicationDbContext _db;

        protected BasePageModel(ApplicationDbContext db)
        {
            _db = db;
        }

        public CurrentUserProfile CurrentUser { get; private set; } = new();

        protected async Task LoadCurrentUserAsync()
        {
            var username = User.Identity?.Name;
            if (string.IsNullOrEmpty(username)) return;

            var emp = await _db.Employees
                .Include(e => e.Designation)
                .Include(e => e.Department)
                .Include(e => e.Branch)
                .FirstOrDefaultAsync(e => e.Email == username);

            if (emp != null)
            {
                CurrentUser = new CurrentUserProfile
                {
                    FullName = $"{emp.FirstName} {emp.LastName}".Trim(),
                    Initials = BuildInitials(emp.FirstName, emp.LastName),
                    Designation = emp.Designation?.Title ?? string.Empty,
                    Department = emp.Department?.Name ?? string.Empty,
                    EmployeeCode = $"EMP-{emp.Id:D5}",
                    Status = emp.Status ?? "Active",
                    PhotoUrl = null
                };
            }
            else
            {
                var displayName = username.Contains('@')
                    ? username.Split('@')[0]
                    : username;

                displayName = string.Join(" ",
                    displayName.Split('.', '_', '-')
                               .Select(w => w.Length > 0
                                   ? char.ToUpper(w[0]) + w[1..]
                                   : w));

                var words = displayName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                var initials = string.Concat(words.Take(2).Select(w => w[0].ToString().ToUpper()));

                var role = User.Claims
                    .FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.Role)?.Value
                    ?? string.Empty;

                CurrentUser = new CurrentUserProfile
                {
                    FullName = displayName,
                    Initials = initials,
                    Designation = role,
                    Department = string.Empty,
                    EmployeeCode = string.Empty,
                    Status = "Active",
                    PhotoUrl = null
                };
            }
        }

        private static string BuildInitials(string first, string last)
        {
            var f = string.IsNullOrWhiteSpace(first) ? "" : first[0].ToString().ToUpper();
            var l = string.IsNullOrWhiteSpace(last) ? "" : last[0].ToString().ToUpper();
            return f + l;
        }
    }

    public class CurrentUserProfile
    {
        public string FullName { get; set; } = string.Empty;
        public string Initials { get; set; } = "?";
        public string Designation { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public string EmployeeCode { get; set; } = string.Empty;
        public string Status { get; set; } = "Active";
        public string? PhotoUrl { get; set; }
    }
}
