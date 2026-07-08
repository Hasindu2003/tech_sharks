using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HRMS.Infrastructure.Identity;
using HRMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HRMS.UI.Pages.Admin.DutyAccounts
{
    [Authorize(Roles = "Admin")]
    public class IndexModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;

        public IndexModel(UserManager<ApplicationUser> userManager, ApplicationDbContext context)
        {
            _userManager = userManager;
            _context = context;
        }

        public List<DutyAccountItem> HRManagers { get; set; } = new();
        public List<DutyAccountItem> AreaManagers { get; set; } = new();
        public List<DutyAccountItem> BranchManagers { get; set; } = new();
        public List<DutyAccountItem> DepartmentHeads { get; set; } = new();

        public async Task OnGetAsync()
        {
            HRManagers = await LoadRoleAsync("HR Manager");
            AreaManagers = await LoadRoleAsync("Area Manager");
            BranchManagers = await LoadRoleAsync("Branch Manager");
            DepartmentHeads = await LoadRoleAsync("Department Head");
        }

        public async Task<IActionResult> OnPostDeleteAsync(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound();

            var displayName = user.FullName;

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                if (user.EmployeeId.HasValue)
                {
                    var employee = await _context.Employees.FindAsync(user.EmployeeId.Value);
                    if (employee != null)
                        _context.Employees.Remove(employee);
                    await _context.SaveChangesAsync();
                }

                await _userManager.DeleteAsync(user);
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }

            TempData["SuccessMessage"] = $"Duty account '{displayName}' has been deleted.";
            return RedirectToPage();
        }

        private async Task<List<DutyAccountItem>> LoadRoleAsync(string role)
        {
            var users = await _userManager.GetUsersInRoleAsync(role);
            var result = new List<DutyAccountItem>();

            foreach (var u in users)
            {
                string managedBranchNames = string.Empty;
                if (role == "Area Manager" && !string.IsNullOrEmpty(u.ManagedBranches))
                {
                    var ids = u.ManagedBranches.Split(',')
                        .Select(s => int.TryParse(s, out var i) ? i : 0)
                        .Where(i => i > 0).ToList();
                    var names = await _context.Branches
                        .Where(b => ids.Contains(b.Id))
                        .OrderBy(b => b.Name)
                        .Select(b => b.Name).ToListAsync();
                    managedBranchNames = string.Join(", ", names);
                }

                result.Add(new DutyAccountItem
                {
                    UserId             = u.Id,
                    FullName           = u.FullName,
                    Email              = u.Email ?? string.Empty,
                    BranchOrArea       = u.Branch ?? "-",
                    Department         = u.Department ?? "-",
                    ManagedBranchNames = managedBranchNames,
                    EmployeeId         = u.EmployeeId,
                });
            }

            return result.OrderBy(r => r.FullName).ToList();
        }

        public class DutyAccountItem
        {
            public string UserId             { get; set; } = string.Empty;
            public string FullName           { get; set; } = string.Empty;
            public string Email              { get; set; } = string.Empty;
            public string BranchOrArea       { get; set; } = string.Empty;
            public string Department         { get; set; } = string.Empty;
            public string ManagedBranchNames { get; set; } = string.Empty;
            public int?   EmployeeId         { get; set; }
        }
    }
}
