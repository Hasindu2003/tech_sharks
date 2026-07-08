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
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace HRMS.UI.Pages.Admin.DutyAccounts
{
    [Authorize(Roles = "Admin")]
    public class EditDutyAccountModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;

        public EditDutyAccountModel(UserManager<ApplicationUser> userManager, ApplicationDbContext context)
        {
            _userManager = userManager;
            _context = context;
        }

        // ── Bound ─────────────────────────────────────────────────────────
        [BindProperty] public string UserId   { get; set; } = string.Empty;
        [BindProperty] public int?   BranchId { get; set; }
        [BindProperty] public string AreaName { get; set; } = string.Empty;
        [BindProperty] public List<int> ManagedBranchIds { get; set; } = new();

        // ── Display ───────────────────────────────────────────────────────
        public string Role               { get; set; } = string.Empty;
        public string CurrentDisplayName { get; set; } = string.Empty;
        public string CurrentEmail       { get; set; } = string.Empty;
        public List<SelectListItem> BranchList { get; set; } = new();

        // ── GET ───────────────────────────────────────────────────────────
        public async Task<IActionResult> OnGetAsync(string userId)
        {
            if (string.IsNullOrEmpty(userId)) return NotFound();

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound();

            var roles = await _userManager.GetRolesAsync(user);
            Role = roles.FirstOrDefault(r => r is "HR Manager" or "Area Manager" or "Branch Manager")
                   ?? string.Empty;
            if (string.IsNullOrEmpty(Role)) return NotFound();

            UserId             = user.Id;
            CurrentDisplayName = user.FullName;
            CurrentEmail       = user.Email ?? string.Empty;

            // Pre-populate fields for re-render after validation failure
            if (Role == "Area Manager")
            {
                AreaName = user.Branch ?? string.Empty;
                if (!string.IsNullOrEmpty(user.ManagedBranches))
                    ManagedBranchIds = user.ManagedBranches.Split(',')
                        .Select(s => int.TryParse(s, out var i) ? i : 0)
                        .Where(i => i > 0).ToList();
            }
            else if (user.EmployeeId.HasValue)
            {
                var emp = await _context.Employees.FindAsync(user.EmployeeId.Value);
                BranchId = emp?.BranchId;
            }

            await LoadBranchListAsync();
            return Page();
        }

        // ── POST ──────────────────────────────────────────────────────────
        public async Task<IActionResult> OnPostAsync()
        {
            var user = await _userManager.FindByIdAsync(UserId);
            if (user == null) return NotFound();

            var roles = await _userManager.GetRolesAsync(user);
            Role = roles.FirstOrDefault(r => r is "HR Manager" or "Area Manager" or "Branch Manager")
                   ?? string.Empty;

            CurrentDisplayName = user.FullName;
            CurrentEmail       = user.Email ?? string.Empty;
            await LoadBranchListAsync();

            // ── Validation ────────────────────────────────────────────────
            switch (Role)
            {
                case "HR Manager":
                {
                    if (!BranchId.HasValue || BranchId == 0)
                    { ModelState.AddModelError("BranchId", "Please select a branch."); break; }

                    // One HR Manager per branch (exclude self)
                    var currentEmpId = user.EmployeeId;
                    var hrIds = (await _userManager.GetUsersInRoleAsync("HR Manager"))
                        .Where(u => u.Id != UserId && u.EmployeeId != null)
                        .Select(u => u.EmployeeId!.Value).ToList();
                    if (hrIds.Count > 0 && await _context.Employees.AnyAsync(e => hrIds.Contains(e.Id) && e.BranchId == BranchId))
                        ModelState.AddModelError("BranchId", "An HR Manager account already exists for this branch.");

                    break;
                }

                case "Branch Manager":
                {
                    if (!BranchId.HasValue || BranchId == 0)
                    { ModelState.AddModelError("BranchId", "Please select a branch."); break; }

                    var bmIds = (await _userManager.GetUsersInRoleAsync("Branch Manager"))
                        .Where(u => u.Id != UserId && u.EmployeeId != null)
                        .Select(u => u.EmployeeId!.Value).ToList();
                    if (bmIds.Count > 0 && await _context.Employees.AnyAsync(e => bmIds.Contains(e.Id) && e.BranchId == BranchId))
                        ModelState.AddModelError("BranchId", "A Branch Manager account already exists for this branch.");
                    break;
                }

                case "Area Manager":
                {
                    if (string.IsNullOrWhiteSpace(AreaName))
                        ModelState.AddModelError("AreaName", "Please enter an area name.");
                    if (!ManagedBranchIds.Any())
                    { ModelState.AddModelError("ManagedBranchIds", "Please select at least one branch."); break; }

                    // Area name uniqueness (exclude self)
                    var proposedName = $"Area Manager - {AreaName.Trim()}";
                    if ((await _userManager.GetUsersInRoleAsync("Area Manager"))
                        .Any(u => u.Id != UserId && u.FullName.Equals(proposedName, StringComparison.OrdinalIgnoreCase)))
                        ModelState.AddModelError("AreaName", "An Area Manager account for this area already exists.");

                    // Branch exclusivity (exclude self's current branches)
                    var selfBranchIds = string.IsNullOrEmpty(user.ManagedBranches)
                        ? new HashSet<int>()
                        : user.ManagedBranches.Split(',')
                            .Select(s => int.TryParse(s, out var i) ? i : 0)
                            .Where(i => i > 0).ToHashSet();

                    var takenBranchIds = new HashSet<int>();
                    foreach (var am in (await _userManager.GetUsersInRoleAsync("Area Manager"))
                             .Where(u => u.Id != UserId && !string.IsNullOrEmpty(u.ManagedBranches)))
                    {
                        foreach (var part in am.ManagedBranches!.Split(','))
                            if (int.TryParse(part, out var bid)) takenBranchIds.Add(bid);
                    }

                    var conflicting = ManagedBranchIds.Where(id => takenBranchIds.Contains(id)).ToList();
                    if (conflicting.Count > 0)
                    {
                        var names = await _context.Branches
                            .Where(b => conflicting.Contains(b.Id)).Select(b => b.Name).ToListAsync();
                        ModelState.AddModelError("ManagedBranchIds",
                            $"Already assigned to another Area Manager: {string.Join(", ", names)}.");
                    }

                    break;
                }
            }

            if (!ModelState.IsValid) return Page();

            // ── Apply Updates ─────────────────────────────────────────────
            var resolvedBranchId = Role == "Area Manager" ? ManagedBranchIds.First() : BranchId!.Value;
            var newDisplayName   = Role switch
            {
                "HR Manager"     => $"HR Manager - {await GetBranchNameAsync(resolvedBranchId)}",
                "Branch Manager" => $"Branch Manager - {await GetBranchNameAsync(resolvedBranchId)}",
                "Area Manager"   => $"Area Manager - {AreaName.Trim()}",
                _                => user.FullName
            };

            // Update Identity user
            user.FullName = newDisplayName;
            user.Branch   = Role == "Area Manager"
                ? AreaName.Trim()
                : await GetBranchNameAsync(resolvedBranchId);
            user.ManagedBranches = Role == "Area Manager"
                ? string.Join(",", ManagedBranchIds)
                : null;
            await _userManager.UpdateAsync(user);

            // Update Employee record
            if (user.EmployeeId.HasValue)
            {
                var emp = await _context.Employees.FindAsync(user.EmployeeId.Value);
                if (emp != null)
                {
                    emp.FullName  = newDisplayName;
                    emp.Initials  = newDisplayName;
                    emp.BranchId  = resolvedBranchId;

                    var newDeptId = await GetFallbackDepartmentIdAsync(resolvedBranchId);
                    if (newDeptId > 0) emp.DepartmentId = newDeptId;

                    await _context.SaveChangesAsync();
                }
            }

            TempData["SuccessMessage"] = $"Duty account updated to '{newDisplayName}'.";
            return RedirectToPage("Index");
        }

        // ── Helpers ───────────────────────────────────────────────────────

        private async Task LoadBranchListAsync()
        {
            var branches = await _context.Branches.OrderBy(b => b.Name).ToListAsync();
            BranchList = branches.Select(b => new SelectListItem
            {
                Value = b.Id.ToString(),
                Text  = b.Name
            }).ToList();
        }

        private async Task<string> GetBranchNameAsync(int branchId)
        {
            var branch = await _context.Branches.FindAsync(branchId);
            return branch?.Name ?? "-";
        }

        private async Task<int> GetFallbackDepartmentIdAsync(int branchId)
        {
            var bd = await _context.BranchDepartments
                .Where(bd => bd.BranchId == branchId).OrderBy(bd => bd.Id).FirstOrDefaultAsync();
            return bd?.DepartmentId ?? 0;
        }
    }
}
