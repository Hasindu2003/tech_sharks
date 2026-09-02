using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
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
    [Authorize(Roles = "Admin,HR Manager")]
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
        [BindProperty] public string UserId        { get; set; } = string.Empty;
        [BindProperty] public int?   BranchId      { get; set; }
        [BindProperty] public int?   DepartmentId  { get; set; }
        [BindProperty] public string? AreaName     { get; set; }
        [BindProperty] public List<int> ManagedBranchIds { get; set; } = new();
        [BindProperty] public string? OfficerName  { get; set; }

        // ── Display ───────────────────────────────────────────────────────
        public string Role               { get; set; } = string.Empty;
        public string CurrentDisplayName { get; set; } = string.Empty;
        public string CurrentUserName    { get; set; } = string.Empty;
        public string CurrentEmail       { get; set; } = string.Empty;
        public List<SelectListItem> BranchList     { get; set; } = new();
        public List<SelectListItem> DepartmentList { get; set; } = new();

        // ── GET ───────────────────────────────────────────────────────────
        public async Task<IActionResult> OnGetAsync(string userId)
        {
            if (string.IsNullOrEmpty(userId)) return NotFound();

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound();

            var roles = await _userManager.GetRolesAsync(user);
            Role = roles.FirstOrDefault(r => r is "HR Officer" or "HR Manager" or "Area Manager" or "Branch Manager" or "Department Head")
                   ?? string.Empty;
            if (string.IsNullOrEmpty(Role)) return NotFound();

            if (Role is "HR Manager" || user.UserName is "hrmanager")
            {
                TempData["ErrorMessage"] = "Corporate HR Manager account cannot be edited.";
                return RedirectToPage("/Admin/DutyAccounts/Index");
            }

            UserId             = user.Id;
            CurrentDisplayName = user.FullName;
            CurrentUserName    = user.UserName ?? string.Empty;
            CurrentEmail       = user.Email ?? string.Empty;

            // Pre-populate fields
            if (Role == "Area Manager")
            {
                AreaName = user.Branch ?? string.Empty;
                if (!string.IsNullOrEmpty(user.ManagedBranches))
                    ManagedBranchIds = user.ManagedBranches.Split(',')
                        .Select(s => int.TryParse(s, out var i) ? i : 0)
                        .Where(i => i > 0).ToList();
            }
            else if (Role == "HR Officer")
            {
                OfficerName = user.FullName.StartsWith("HR Officer - ") 
                    ? user.FullName.Replace("HR Officer - ", "") 
                    : user.FullName;
                if (!string.IsNullOrEmpty(user.ManagedBranches))
                    ManagedBranchIds = user.ManagedBranches.Split(',')
                        .Select(s => int.TryParse(s, out var i) ? i : 0)
                        .Where(i => i > 0).ToList();
            }
            else if (user.EmployeeId.HasValue)
            {
                var emp = await _context.Employees.FindAsync(user.EmployeeId.Value);
                BranchId = emp?.BranchId;
                DepartmentId = emp?.DepartmentId;
            }

            await LoadDropdownsAsync();
            return Page();
        }

        // ── POST ──────────────────────────────────────────────────────────
        public async Task<IActionResult> OnPostAsync()
        {
            var user = await _userManager.FindByIdAsync(UserId);
            if (user == null) return NotFound();

            var roles = await _userManager.GetRolesAsync(user);
            Role = roles.FirstOrDefault(r => r is "HR Officer" or "HR Manager" or "Area Manager" or "Branch Manager" or "Department Head")
                   ?? string.Empty;

            if (Role is "HR Manager" || user.UserName is "hrmanager")
            {
                TempData["ErrorMessage"] = "Corporate HR Manager account cannot be edited.";
                return RedirectToPage("/Admin/DutyAccounts/Index");
            }

            CurrentDisplayName = user.FullName;
            CurrentEmail       = user.Email ?? string.Empty;
            await LoadDropdownsAsync();

            // Remove non-applicable fields from validation based on role
            if (Role != "HR Officer")
            {
                ModelState.Remove("OfficerName");
            }
            if (Role != "Area Manager")
            {
                ModelState.Remove("AreaName");
                ModelState.Remove("ManagedBranchIds");
            }
            if (Role != "Branch Manager" && Role != "Department Head")
            {
                ModelState.Remove("BranchId");
            }
            if (Role != "Department Head")
            {
                ModelState.Remove("DepartmentId");
            }

            // ── Validation ────────────────────────────────────────────────
            switch (Role)
            {
                case "HR Officer":
                {
                    if (string.IsNullOrWhiteSpace(OfficerName))
                        ModelState.AddModelError("OfficerName", "Please enter officer name or identifier.");
                    break;
                }

                case "HR Manager":
                case "Welfare Manager":
                case "Head of Welfare":
                {
                    break;
                }

                case "Branch Manager":
                {
                    if (!BranchId.HasValue || BranchId == 0)
                    { 
                        ModelState.AddModelError("BranchId", "Please select a branch."); 
                        break; 
                    }

                    var bmIds = (await _userManager.GetUsersInRoleAsync("Branch Manager"))
                        .Where(u => u.Id != UserId && u.EmployeeId != null)
                        .Select(u => u.EmployeeId!.Value).ToList();
                    if (bmIds.Count > 0 && await _context.Employees.AnyAsync(e => bmIds.Contains(e.Id) && e.BranchId == BranchId))
                        ModelState.AddModelError("BranchId", "A Branch Manager account already exists for this branch.");
                    break;
                }

                case "Department Head":
                {
                    if (!BranchId.HasValue || BranchId == 0)
                    {
                        ModelState.AddModelError("BranchId", "Please select a branch.");
                    }
                    if (!DepartmentId.HasValue || DepartmentId == 0)
                    {
                        ModelState.AddModelError("DepartmentId", "Please select a department.");
                    }
                    if (BranchId.HasValue && DepartmentId.HasValue)
                    {
                        var dhIds = (await _userManager.GetUsersInRoleAsync("Department Head"))
                            .Where(u => u.Id != UserId && u.EmployeeId != null)
                            .Select(u => u.EmployeeId!.Value).ToList();
                        if (dhIds.Count > 0 && await _context.Employees.AnyAsync(e => dhIds.Contains(e.Id) && e.BranchId == BranchId.Value && e.DepartmentId == DepartmentId.Value))
                        {
                            ModelState.AddModelError("DepartmentId", "A Department Head account already exists for this branch and department.");
                        }
                    }
                    break;
                }

                case "Area Manager":
                {
                    if (string.IsNullOrWhiteSpace(AreaName))
                    {
                        ModelState.AddModelError("AreaName", "Please enter an area name (e.g. Central Province, Western Region).");
                    }
                    if (!ManagedBranchIds.Any())
                    {
                        ModelState.AddModelError("ManagedBranchIds", "Please select at least one branch for the Area Manager to oversee.");
                    }
                    if (string.IsNullOrWhiteSpace(AreaName) || !ManagedBranchIds.Any())
                    {
                        break;
                    }

                    var proposedName = $"Area Manager - {AreaName.Trim()}";
                    if ((await _userManager.GetUsersInRoleAsync("Area Manager"))
                        .Any(u => u.Id != UserId && u.FullName.Equals(proposedName, StringComparison.OrdinalIgnoreCase)))
                    {
                        ModelState.AddModelError("AreaName", "An Area Manager account for this area already exists.");
                    }

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
            var headOffice = await _context.Branches.FirstOrDefaultAsync(b => b.Name == "Head Office" || b.Name == "Head Office - Colombo");
            var resolvedBranchId = Role switch
            {
                "Area Manager"    => ManagedBranchIds.First(),
                "HR Officer"      => headOffice?.Id ?? (BranchId ?? 1),
                "HR Manager"      => headOffice?.Id ?? (BranchId ?? 1),
                _                 => BranchId!.Value
            };

            var branchName = await GetBranchNameAsync(resolvedBranchId);
            var deptName   = DepartmentId.HasValue ? await GetDeptNameAsync(DepartmentId.Value) : string.Empty;

            var newDisplayName = Role switch
            {
                "HR Officer"      => $"HR Officer - {OfficerName?.Trim()}",
                "HR Manager"      => "HR Manager",
                "Branch Manager"  => $"Branch Manager - {branchName}",
                "Area Manager"    => $"Area Manager - {AreaName?.Trim()}",
                "Department Head" => $"Department Head - {deptName} - {branchName}",
                _                 => user.FullName
            };

            if (newDisplayName.Length > 100) newDisplayName = newDisplayName.Substring(0, 100);

            // Update Identity user
            user.FullName = newDisplayName;
            user.Branch   = Role == "Area Manager"
                ? AreaName?.Trim() ?? "General"
                : (Role == "HR Officer" || Role == "HR Manager")
                    ? (headOffice?.Name ?? "Head Office")
                    : branchName;

            user.Department = Role == "Department Head"
                ? deptName
                : user.Department;

            user.ManagedBranches = (Role == "Area Manager" || Role == "HR Officer")
                ? (ManagedBranchIds.Any() ? string.Join(",", ManagedBranchIds) : null)
                : null;

            if (Role == "Area Manager" && !string.IsNullOrWhiteSpace(AreaName))
            {
                var cleanArea = Regex.Replace(AreaName.Trim().ToLowerInvariant(), @"[^a-z0-9]", "");
                user.UserName = $"am.{cleanArea}";
                user.Email = $"{user.UserName}@kanrich.lk";
            }
            else if (Role == "Branch Manager")
            {
                var cleanBranch = Regex.Replace(branchName.ToLowerInvariant(), @"[^a-z0-9]", "");
                user.UserName = $"bm.{cleanBranch}";
                user.Email = $"{user.UserName}@kanrich.lk";
            }
            else if (Role == "Department Head" && !string.IsNullOrEmpty(deptName) && !string.IsNullOrEmpty(branchName))
            {
                var cleanDept = Regex.Replace(deptName.ToLowerInvariant(), @"[^a-z0-9]", "");
                var cleanBranch = Regex.Replace(branchName.ToLowerInvariant(), @"[^a-z0-9]", "");
                user.UserName = $"dh.{cleanDept}{cleanBranch}";
                user.Email = $"{user.UserName}@kanrich.lk";
            }

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
                    if (Role == "Department Head" && DepartmentId.HasValue)
                    {
                        emp.DepartmentId = DepartmentId.Value;
                    }
                    emp.Email     = user.Email ?? emp.Email;
                    await _context.SaveChangesAsync();
                }
            }

            TempData["SuccessMessage"] = $"Duty account updated to '{newDisplayName}'.";
            return RedirectToPage("Index");
        }

        // ── Helpers ───────────────────────────────────────────────────────

        private async Task LoadDropdownsAsync()
        {
            var branches = await _context.Branches.OrderBy(b => b.Name).ToListAsync();
            BranchList = branches.Select(b => new SelectListItem
            {
                Value = b.Id.ToString(),
                Text  = string.IsNullOrEmpty(b.Location) ? b.Name : $"{b.Name} ({b.Location})"
            }).ToList();

            var depts = await _context.Departments
                .Where(d => d.Name != "Managerial" && d.Name != "Management")
                .OrderBy(d => d.Name)
                .ToListAsync();
            DepartmentList = depts.Select(d => new SelectListItem
            {
                Value = d.Id.ToString(),
                Text  = d.Name
            }).ToList();
        }

        private async Task<string> GetBranchNameAsync(int branchId)
        {
            var branch = await _context.Branches.FindAsync(branchId);
            return branch?.Name ?? "-";
        }

        private async Task<string> GetDeptNameAsync(int deptId)
        {
            var dept = await _context.Departments.FindAsync(deptId);
            return dept?.Name ?? "-";
        }
    }
}
