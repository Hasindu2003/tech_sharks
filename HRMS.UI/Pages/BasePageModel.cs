using HRMS.Domain.Entities.Core;
using HRMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

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

            var userAccount = await _db.Users.FirstOrDefaultAsync(u => u.UserName == username || u.Email == username);
            HRMS.Domain.Entities.Core.Employee? emp = null;

            if (userAccount?.EmployeeId.HasValue == true)
            {
                emp = await _db.Employees
                    .Include(e => e.Designation)
                    .Include(e => e.Department)
                    .Include(e => e.Branch)
                    .FirstOrDefaultAsync(e => e.Id == userAccount.EmployeeId.Value);
            }

            if (emp == null && !string.IsNullOrEmpty(userAccount?.Email))
            {
                emp = await _db.Employees
                    .Include(e => e.Designation)
                    .Include(e => e.Department)
                    .Include(e => e.Branch)
                    .FirstOrDefaultAsync(e => e.Email == userAccount.Email);
            }

            if (emp == null)
            {
                emp = await _db.Employees
                    .Include(e => e.Designation)
                    .Include(e => e.Department)
                    .Include(e => e.Branch)
                    .FirstOrDefaultAsync(e => e.Email == username);
            }

            if (emp != null)
            {
                CurrentUser = new CurrentUserProfile
                {
                    FullName = emp.FullName ?? string.Empty,
                    Initials = emp.Initials ?? "?",
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
