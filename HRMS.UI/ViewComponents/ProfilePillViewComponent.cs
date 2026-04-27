using HRMS.Infrastructure.Persistence;
using HRMS.UI.Pages;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HRMS.UI.ViewComponents
{
    public class ProfilePillViewComponent : ViewComponent
    {
        private readonly ApplicationDbContext _db;

        public ProfilePillViewComponent(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var username = UserClaimsPrincipal.Identity?.Name;

            if (string.IsNullOrEmpty(username))
                return View(new CurrentUserProfile());

            var emp = await _db.Employees
                .Include(e => e.Designation)
                .Include(e => e.Department)
                .Include(e => e.Branch)
                .FirstOrDefaultAsync(e => e.Email == username);

            CurrentUserProfile profile;

            if (emp != null)
            {
                profile = new CurrentUserProfile
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

                var role = UserClaimsPrincipal.Claims
                    .FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.Role)?.Value
                    ?? string.Empty;

                profile = new CurrentUserProfile
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

            return View(profile);
        }

        private static string BuildInitials(string first, string last)
        {
            var f = string.IsNullOrWhiteSpace(first) ? "" : first[0].ToString().ToUpper();
            var l = string.IsNullOrWhiteSpace(last) ? "" : last[0].ToString().ToUpper();
            return f + l;
        }
    }
}
