using System;
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
    [Authorize(Roles = "Admin,HR Manager")]
    public class IndexModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;

        public IndexModel(UserManager<ApplicationUser> userManager, ApplicationDbContext context)
        {
            _userManager = userManager;
            _context = context;
        }

        public List<DutyAccountItem> CorporateHeads { get; set; } = new();
        public List<DutyAccountItem> WelfareHeads { get; set; } = new();
        public List<DutyAccountItem> AreaManagers { get; set; } = new();
        public List<DutyAccountItem> BranchManagers { get; set; } = new();
        public List<DutyAccountItem> DepartmentHeads { get; set; } = new();

        public async Task OnGetAsync()
        {
            var hrManagers = await LoadRoleAsync("HR Manager");
            var welfareManagers = await LoadRoleAsync("Welfare Manager");
            var dhList = await LoadRoleAsync("Department Head");

            CorporateHeads = hrManagers.ToList();
            WelfareHeads = welfareManagers
                .Concat(dhList.Where(d => d.Department == "Welfare" || d.UserName == "head.welfare"))
                .GroupBy(d => d.UserId)
                .Select(g => g.First())
                .ToList();

            AreaManagers = await LoadRoleAsync("Area Manager");
            BranchManagers = await LoadRoleAsync("Branch Manager");
            DepartmentHeads = dhList
                .Where(d => d.Department != "Welfare" && d.UserName != "head.welfare" && !WelfareHeads.Any(w => w.UserId == d.UserId))
                .OrderBy(d => d.FullName)
                .ToList();
        }

        public async Task<IActionResult> OnPostDeleteAsync(string userId)
        {
            if (string.IsNullOrEmpty(userId)) return NotFound();

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound();

            var roles = await _userManager.GetRolesAsync(user);
            if (roles.Contains("HR Manager") || user.UserName is "hrmanager")
            {
                TempData["ErrorMessage"] = "Corporate HR Manager account cannot be deleted.";
                return RedirectToPage();
            }

            var displayName = user.FullName;
            var empId = user.EmployeeId;

            try
            {
                // Delete linked dummy employee record if one was created for this duty account
                if (empId.HasValue)
                {
                    var employee = await _context.Employees.FindAsync(empId.Value);
                    if (employee != null && employee.NIC.StartsWith("DUTY-"))
                    {
                        _context.Employees.Remove(employee);
                        await _context.SaveChangesAsync();
                    }
                }

                // Delete Identity user using UserManager
                var result = await _userManager.DeleteAsync(user);
                if (!result.Succeeded)
                {
                    TempData["ErrorMessage"] = $"Failed to delete duty account: {string.Join(", ", result.Errors.Select(e => e.Description))}";
                    return RedirectToPage();
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Failed to delete duty account: {ex.Message}";
                return RedirectToPage();
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
                if ((role == "Area Manager" || role == "HR Officer") && !string.IsNullOrEmpty(u.ManagedBranches))
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
                    UserName           = u.UserName ?? string.Empty,
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
            public string UserName           { get; set; } = string.Empty;
            public string FullName           { get; set; } = string.Empty;
            public string Email              { get; set; } = string.Empty;
            public string BranchOrArea       { get; set; } = string.Empty;
            public string Department         { get; set; } = string.Empty;
            public string ManagedBranchNames { get; set; } = string.Empty;
            public int?   EmployeeId         { get; set; }
        }
    }
}
