using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using HRMS.Domain.Entities.Core;
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
    using Employee = HRMS.Domain.Entities.Core.Employee;

    public class BranchDeptGroup

    {
        public string BranchName { get; set; } = string.Empty;
        public List<SelectListItem> Departments { get; set; } = new();
    }

    [Authorize(Roles = "Admin")]
    public class CreateDutyAccountModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public CreateDutyAccountModel(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // ── Bound Properties ─────────────────────────────────────────────
        [BindProperty]
        public string SelectedRole { get; set; } = string.Empty;

        // Display name is auto-generated; not bound from form input.
        public string AccountDisplayName { get; set; } = string.Empty;

        /// <summary>Used by HR Manager and Branch Manager (single branch).</summary>
        [BindProperty]
        public int? BranchId { get; set; }

        /// <summary>Used by HR Manager only (department within branch).</summary>
        [BindProperty]
        public int? DepartmentId { get; set; }

        /// <summary>Used by Area Manager (multiple branches they oversee).</summary>
        [BindProperty]
        public List<int> ManagedBranchIds { get; set; } = new();

        /// <summary>Used by Area Manager to build the display name.</summary>
        [BindProperty]
        public string AreaName { get; set; } = string.Empty;

        /// <summary>Used by Department Head (BranchDepartment record ID).</summary>
        [BindProperty]
        public int? DeptHeadBranchDeptId { get; set; }

        // ── Dropdown Data ─────────────────────────────────────────────────
        public List<SelectListItem> BranchList { get; set; } = new();
        public List<SelectListItem> HRManagerBranchList { get; set; } = new();
        public List<SelectListItem> BranchManagerBranchList { get; set; } = new();
        public List<SelectListItem> AreaManagerBranchList { get; set; } = new();
        public List<SelectListItem> DepartmentList { get; set; } = new();
        public List<SelectListItem> DeptHeadBranchDeptList { get; set; } = new();
        public List<BranchDeptGroup> DeptHeadBranchGroups { get; set; } = new();

        // ── GET ───────────────────────────────────────────────────────────
        public async Task<IActionResult> OnGetAsync()
        {
            await LoadDropdownsAsync();
            return Page();
        }

        // ── POST ──────────────────────────────────────────────────────────
        public async Task<IActionResult> OnPostAsync()
        {
            await LoadDropdownsAsync();

            // Remove fields that don't apply to the selected role so their
            // empty values don't trigger model validation errors.
            if (SelectedRole != "Area Manager")
            {
                ModelState.Remove("AreaName");
                ModelState.Remove("ManagedBranchIds");
            }
            if (SelectedRole == "Area Manager")
            {
                ModelState.Remove("BranchId");
            }
            if (SelectedRole != "Department Head")
            {
                ModelState.Remove("DeptHeadBranchDeptId");
            }

            // --- Prerequisite checks (before any role logic) ---
            if (!await _context.Designations.AnyAsync())
            {
                ModelState.AddModelError("", "No designations exist yet. Please create at least one designation under Settings → Designations first.");
                return Page();
            }

            if (string.IsNullOrWhiteSpace(SelectedRole))
            {
                ModelState.AddModelError("SelectedRole", "Please select a duty account type.");
                return Page();
            }

            // Resolve the BranchDepartment record for Department Head early (used in both validation and creation)
            BranchDepartment? deptHeadBD = null;
            if (SelectedRole == "Department Head" && DeptHeadBranchDeptId is > 0)
            {
                deptHeadBD = await _context.BranchDepartments
                    .Include(bd => bd.Branch)
                    .Include(bd => bd.Department)
                    .FirstOrDefaultAsync(bd => bd.Id == DeptHeadBranchDeptId.Value);
            }

            // --- Role-specific validation ---
            switch (SelectedRole)
            {
                case "HR Manager":
                {
                    if (!BranchId.HasValue || BranchId == 0)
                    {
                        ModelState.AddModelError("BranchId", "Please select a branch.");
                        break;
                    }
                    // One HR Manager per branch
                    var hrIds = (await _userManager.GetUsersInRoleAsync("HR Manager"))
                        .Where(u => u.EmployeeId != null).Select(u => u.EmployeeId!.Value).ToList();
                    if (hrIds.Count > 0 && await _context.Employees.AnyAsync(e => hrIds.Contains(e.Id) && e.BranchId == BranchId))
                        ModelState.AddModelError("BranchId", "An HR Manager account already exists for this branch.");
                    break;
                }

                case "Area Manager":
                {
                    if (string.IsNullOrWhiteSpace(AreaName))
                        ModelState.AddModelError("AreaName", "Please enter an area name.");
                    if (!ManagedBranchIds.Any())
                    {
                        ModelState.AddModelError("ManagedBranchIds", "Please select at least one branch.");
                        break;
                    }
                    // Area name must be unique
                    var proposedName = $"Area Manager - {AreaName.Trim()}";
                    if ((await _userManager.GetUsersInRoleAsync("Area Manager"))
                        .Any(u => u.FullName.Equals(proposedName, StringComparison.OrdinalIgnoreCase)))
                        ModelState.AddModelError("AreaName", "An Area Manager account for this area already exists.");
                    // No branch can belong to more than one Area Manager
                    var takenBranchIds = new HashSet<int>();
                    foreach (var am in (await _userManager.GetUsersInRoleAsync("Area Manager"))
                             .Where(u => !string.IsNullOrEmpty(u.ManagedBranches)))
                    {
                        foreach (var part in am.ManagedBranches!.Split(','))
                            if (int.TryParse(part, out var bid)) takenBranchIds.Add(bid);
                    }
                    var conflicting = ManagedBranchIds.Where(id => takenBranchIds.Contains(id)).ToList();
                    if (conflicting.Count > 0)
                    {
                        var names = await _context.Branches
                            .Where(b => conflicting.Contains(b.Id))
                            .Select(b => b.Name).ToListAsync();
                        ModelState.AddModelError("ManagedBranchIds",
                            $"Already assigned to another Area Manager: {string.Join(", ", names)}.");
                    }
                    break;
                }

                case "Branch Manager":
                {
                    if (!BranchId.HasValue || BranchId == 0)
                    {
                        ModelState.AddModelError("BranchId", "Please select a branch.");
                        break;
                    }
                    // One Branch Manager per branch
                    var bmIds = (await _userManager.GetUsersInRoleAsync("Branch Manager"))
                        .Where(u => u.EmployeeId != null).Select(u => u.EmployeeId!.Value).ToList();
                    if (bmIds.Count > 0 && await _context.Employees.AnyAsync(e => bmIds.Contains(e.Id) && e.BranchId == BranchId))
                        ModelState.AddModelError("BranchId", "A Branch Manager account already exists for this branch.");
                    break;
                }

                case "Department Head":
                {
                    if (!DeptHeadBranchDeptId.HasValue || DeptHeadBranchDeptId == 0)
                    {
                        ModelState.AddModelError("DeptHeadBranchDeptId", "Please select a branch and department.");
                        break;
                    }
                    if (deptHeadBD == null)
                    {
                        ModelState.AddModelError("DeptHeadBranchDeptId", "Invalid selection.");
                        break;
                    }
                    var dhIds = (await _userManager.GetUsersInRoleAsync("Department Head"))
                        .Where(u => u.EmployeeId != null).Select(u => u.EmployeeId!.Value).ToList();
                    if (dhIds.Count > 0 && await _context.Employees.AnyAsync(
                            e => dhIds.Contains(e.Id) && e.BranchId == deptHeadBD.BranchId && e.DepartmentId == deptHeadBD.DepartmentId))
                        ModelState.AddModelError("DeptHeadBranchDeptId",
                            "A Department Head already exists for this branch and department combination.");
                    break;
                }

                default:
                    ModelState.AddModelError("SelectedRole", "Invalid role selected.");
                    break;
            }

            if (!ModelState.IsValid)
                return Page();

            // --- Auto-generate display name ---
            var branchName = BranchId.HasValue ? await GetBranchNameAsync(BranchId.Value) : string.Empty;
            AccountDisplayName = SelectedRole switch
            {
                "HR Manager"      => $"HR Manager - {branchName}",
                "Branch Manager"  => $"Branch Manager - {branchName}",
                "Area Manager"    => $"Area Manager - {AreaName.Trim()}",
                "Department Head" => $"Department Head - {deptHeadBD!.Department.Name} - {deptHeadBD!.Branch.Name}",
                _                 => AccountDisplayName
            };

            // --- Resolve branch / department / designation IDs ---
            var resolvedBranchId = SelectedRole switch
            {
                "Area Manager"    => ManagedBranchIds.First(),
                "Department Head" => deptHeadBD!.BranchId,
                _                 => BranchId!.Value
            };

            var resolvedDeptId = SelectedRole == "Department Head"
                ? deptHeadBD!.DepartmentId
                : await GetFallbackDepartmentIdAsync(resolvedBranchId);
            var resolvedDesigId = resolvedDeptId > 0 ? await GetFallbackDesignationIdAsync(resolvedDeptId) : 0;

            var email = GenerateDutyEmail(SelectedRole, AccountDisplayName);

            // --- Wrap both saves in a transaction so nothing is orphaned on failure ---
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var employee = new Employee
                {
                    FullName           = AccountDisplayName,
                    Initials           = AccountDisplayName,
                    NIC                = "DUTY-ACC",
                    DateOfBirth        = new DateTime(1900, 1, 1),
                    Sex                = "N/A",
                    PhoneNumber        = "0000000000",
                    ResidentialAddress = "-",
                    EmployeeType       = "Permanent",
                    EPFNumber          = "N/A",
                    ETFNumber          = "N/A",
                    BankAccountName    = "-",
                    BankAccountNumber  = "-",
                    Email              = email,
                    DateJoined         = DateTime.Now,
                    Status             = "Active",
                    DepartmentId       = resolvedDeptId  > 0 ? resolvedDeptId  : null,
                    DesignationId      = resolvedDesigId > 0 ? resolvedDesigId : null,
                    BranchId           = resolvedBranchId,
                };

                _context.Employees.Add(employee);
                await _context.SaveChangesAsync();

                string password = $"Kanrich@{new Random().Next(1000, 9999)}";
                var user = new ApplicationUser
                {
                    UserName        = email,
                    Email           = email,
                    EmailConfirmed  = true,
                    EmployeeId      = employee.Id,
                    FullName        = AccountDisplayName,
                    EpfNumber       = "N/A",
                    Branch          = SelectedRole == "Area Manager"
                                        ? AreaName.Trim()
                                        : await GetBranchNameAsync(resolvedBranchId),
                    Department      = SelectedRole == "Department Head"
                                        ? deptHeadBD!.Department.Name
                                        : null,
                    Designation     = SelectedRole,
                    DateOfJoining   = DateTime.Now,
                    ManagedBranches = SelectedRole == "Area Manager"
                                        ? string.Join(",", ManagedBranchIds)
                                        : null,
                };

                var result = await _userManager.CreateAsync(user, password);
                if (!result.Succeeded)
                {
                    await transaction.RollbackAsync();
                    foreach (var err in result.Errors)
                        ModelState.AddModelError("", err.Description);
                    return Page();
                }

                await _userManager.AddToRoleAsync(user, SelectedRole);

                var managedInfo = SelectedRole == "Area Manager" && ManagedBranchIds.Count > 1
                    ? $" ({ManagedBranchIds.Count} branches)"
                    : string.Empty;
                var successMsg = $"Duty account '{AccountDisplayName}' ({SelectedRole}) created successfully.{managedInfo}";

                _context.Notifications.Add(new Notification
                {
                    UserId    = _userManager.GetUserId(User) ?? "",
                    Title     = "Duty Account Created",
                    Message   = $"{successMsg}\nEmail: {email}\nPassword: {password}",
                    TargetUrl = $"/Employees/Details/{employee.Id}",
                    IsRead    = false,
                    CreatedAt = DateTime.Now,
                });
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                TempData["SuccessMessage"] = successMsg;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }

            return RedirectToPage("/Admin/DutyAccounts/Create");
        }

        // ── Helpers ───────────────────────────────────────────────────────

        private async Task LoadDropdownsAsync()
        {
            var branches = await _context.Branches.OrderBy(b => b.Name).ToListAsync();
            var departments = await _context.Departments.OrderBy(d => d.Name).ToListAsync();

            BranchList = branches.Select(b => new SelectListItem
            {
                Value = b.Id.ToString(),
                Text  = b.Name
            }).ToList();

            // Collect branch IDs already assigned to an HR Manager
            var hrManagerEmployeeIds = (await _userManager.GetUsersInRoleAsync("HR Manager"))
                .Where(u => u.EmployeeId != null).Select(u => u.EmployeeId!.Value).ToList();
            var hrTakenBranchIds = hrManagerEmployeeIds.Count > 0
                ? (await _context.Employees
                    .Where(e => hrManagerEmployeeIds.Contains(e.Id))
                    .Select(e => e.BranchId).ToListAsync()).ToHashSet()
                : new HashSet<int>();

            HRManagerBranchList = branches
                .Where(b => !hrTakenBranchIds.Contains(b.Id))
                .Select(b => new SelectListItem { Value = b.Id.ToString(), Text = b.Name })
                .ToList();

            // Collect branch IDs already assigned to a Branch Manager
            var branchManagerEmployeeIds = (await _userManager.GetUsersInRoleAsync("Branch Manager"))
                .Where(u => u.EmployeeId != null).Select(u => u.EmployeeId!.Value).ToList();
            var bmTakenBranchIds = branchManagerEmployeeIds.Count > 0
                ? (await _context.Employees
                    .Where(e => branchManagerEmployeeIds.Contains(e.Id))
                    .Select(e => e.BranchId).ToListAsync()).ToHashSet()
                : new HashSet<int>();

            BranchManagerBranchList = branches
                .Where(b => !bmTakenBranchIds.Contains(b.Id))
                .Select(b => new SelectListItem { Value = b.Id.ToString(), Text = b.Name })
                .ToList();

            // Collect branch IDs already assigned to any area manager
            var takenBranchIds = new HashSet<int>();
            foreach (var am in (await _userManager.GetUsersInRoleAsync("Area Manager"))
                     .Where(u => !string.IsNullOrEmpty(u.ManagedBranches)))
            {
                foreach (var part in am.ManagedBranches!.Split(','))
                    if (int.TryParse(part.Trim(), out var bid))
                        takenBranchIds.Add(bid);
            }

            AreaManagerBranchList = branches
                .Where(b => !takenBranchIds.Contains(b.Id))
                .Select(b => new SelectListItem { Value = b.Id.ToString(), Text = b.Name })
                .ToList();

            DepartmentList = departments.Select(d => new SelectListItem
            {
                Value = d.Id.ToString(),
                Text  = d.Name
            }).ToList();

            // Load available branch-dept combos for Department Head (exclude already-taken ones)
            var deptHeadEmpIds = (await _userManager.GetUsersInRoleAsync("Department Head"))
                .Where(u => u.EmployeeId != null).Select(u => u.EmployeeId!.Value).ToList();

            var takenDHPairs = new HashSet<(int, int)>();
            if (deptHeadEmpIds.Count > 0)
            {
                var pairs = await _context.Employees
                    .Where(e => deptHeadEmpIds.Contains(e.Id) && e.DepartmentId != null)
                    .Select(e => new { e.BranchId, DeptId = e.DepartmentId!.Value })
                    .ToListAsync();
                foreach (var p in pairs)
                    takenDHPairs.Add((p.BranchId, p.DeptId));
            }

            var allBranchDepts = await _context.BranchDepartments
                .Include(bd => bd.Branch)
                .Include(bd => bd.Department)
                .OrderBy(bd => bd.Branch.Name).ThenBy(bd => bd.Department.Name)
                .ToListAsync();

            DeptHeadBranchDeptList = allBranchDepts
                .Where(bd => !takenDHPairs.Contains((bd.BranchId, bd.DepartmentId)))
                .Select(bd => new SelectListItem
                {
                    Value = bd.Id.ToString(),
                    Text  = $"{bd.Department.Name} — {bd.Branch.Name}"
                }).ToList();

            DeptHeadBranchGroups = allBranchDepts
                .Where(bd => !takenDHPairs.Contains((bd.BranchId, bd.DepartmentId)))
                .GroupBy(bd => bd.Branch.Name)
                .OrderBy(g => g.Key)
                .Select(g => new BranchDeptGroup
                {
                    BranchName = g.Key,
                    Departments = g.OrderBy(bd => bd.Department.Name)
                        .Select(bd => new SelectListItem
                        {
                            Value = bd.Id.ToString(),
                            Text  = bd.Department.Name
                        }).ToList()
                }).ToList();
        }

        private string GenerateDutyEmail(string role, string displayName)
        {
            // Build slug: "Branch Manager – Kandy" → "branchmanager.kandy"
            var roleSlug = Regex.Replace(role.ToLowerInvariant(), @"\s+", "");

            // Extract the part after a dash/separator if present, otherwise use whole name
            var namePart = displayName;
            var separators = new[] { "–", "-", "—", ":" };
            foreach (var sep in separators)
            {
                var idx = displayName.IndexOf(sep, StringComparison.Ordinal);
                if (idx >= 0)
                {
                    namePart = displayName.Substring(idx + sep.Length).Trim();
                    break;
                }
            }

            var nameSlug = Regex.Replace(namePart.ToLowerInvariant(), @"[^a-z0-9]", "");
            if (string.IsNullOrEmpty(nameSlug)) nameSlug = Regex.Replace(displayName.ToLowerInvariant(), @"[^a-z0-9]", "");

            var baseEmail = $"{roleSlug}.{nameSlug}@kanrich.lk";

            // Ensure uniqueness — append a number if taken
            var counter = 1;
            var email = baseEmail;
            while (_context.Employees.Any(e => e.Email == email))
            {
                email = $"{roleSlug}.{nameSlug}{counter}@kanrich.lk";
                counter++;
            }
            return email;
        }

        private async Task<int> GetFallbackDepartmentIdAsync(int branchId)
        {
            var bd = await _context.BranchDepartments
                .Where(bd => bd.BranchId == branchId)
                .OrderBy(bd => bd.Id)
                .FirstOrDefaultAsync();
            return bd?.DepartmentId ?? 0;
        }

        private async Task<int> GetFallbackDesignationIdAsync(int departmentId)
        {
            var dd = await _context.DepartmentDesignations
                .Where(dd => dd.DepartmentId == departmentId)
                .OrderBy(dd => dd.Id)
                .FirstOrDefaultAsync();
            return dd?.DesignationId ?? 0;
        }

        private async Task<string> GetBranchNameAsync(int branchId)
        {
            var branch = await _context.Branches.FindAsync(branchId);
            return branch?.Name ?? "-";
        }
    }
}
