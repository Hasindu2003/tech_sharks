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

    [Authorize(Roles = "Admin,HR Manager")]
    public class CreateDutyAccountModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public CreateDutyAccountModel(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager)
        {
            _context = context;
            _userManager = userManager;
            _roleManager = roleManager;
        }

        // ── Bound Properties ─────────────────────────────────────────────
        [BindProperty]
        public string SelectedRole { get; set; } = string.Empty;

        public string AccountDisplayName { get; set; } = string.Empty;

        /// <summary>Used by Branch Manager (single branch).</summary>
        [BindProperty]
        public int? BranchId { get; set; }

        /// <summary>Used by Area Manager (multiple branches they oversee).</summary>
        [BindProperty]
        public List<int> ManagedBranchIds { get; set; } = new();

        /// <summary>Used by Area Manager to build the display name.</summary>
        [BindProperty]
        public string AreaName { get; set; } = string.Empty;

        /// <summary>Used by Department Head (BranchDepartment record ID).</summary>
        [BindProperty]
        public int? DeptHeadBranchDeptId { get; set; }

        public bool HasExistingHrManager { get; set; }

        // ── Dropdown Data ─────────────────────────────────────────────────
        public List<SelectListItem> BranchList { get; set; } = new();
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

            // Remove fields that don't apply to the selected role
            if (SelectedRole != "Area Manager")
            {
                ModelState.Remove("AreaName");
                ModelState.Remove("ManagedBranchIds");
            }
            if (SelectedRole == "Area Manager" || SelectedRole == "HR Manager" || SelectedRole == "Department Head")
            {
                ModelState.Remove("BranchId");
            }
            if (SelectedRole != "Department Head")
            {
                ModelState.Remove("DeptHeadBranchDeptId");
            }

            // Ensure core designations exist in DB
            await EnsureCoreDesignationsAsync();

            if (string.IsNullOrWhiteSpace(SelectedRole))
            {
                ModelState.AddModelError("SelectedRole", "Please select a duty account type.");
                return Page();
            }

            // Resolve the BranchDepartment record for Department Head
            BranchDepartment? deptHeadBD = null;
            if (SelectedRole == "Department Head" && DeptHeadBranchDeptId is > 0)
            {
                deptHeadBD = await _context.BranchDepartments
                    .Include(bd => bd.Branch)
                    .Include(bd => bd.Department)
                    .FirstOrDefaultAsync(bd => bd.Id == DeptHeadBranchDeptId.Value);

                if (deptHeadBD == null)
                {
                    ModelState.AddModelError("DeptHeadBranchDeptId", "Selected branch and department combination is invalid.");
                    return Page();
                }

                if (deptHeadBD.Department.Name.Equals("Managerial", StringComparison.OrdinalIgnoreCase) ||
                    deptHeadBD.Department.Name.Equals("Management", StringComparison.OrdinalIgnoreCase))
                {
                    ModelState.AddModelError("DeptHeadBranchDeptId", "Department Head accounts cannot be created for the Managerial department.");
                    return Page();
                }
            }

            // Resolve Head Office & HR Departments
            var headOffice = await _context.Branches.FirstOrDefaultAsync(b => b.Name == "Head Office" || b.Name == "Head Office - Colombo" || b.Name.Contains("Head Office"))
                             ?? (await _context.Branches.FirstOrDefaultAsync());
            var hrDept = await _context.Departments.FirstOrDefaultAsync(d => d.Name == "Human Resources" || d.Name == "HR")
                         ?? (await _context.Departments.FirstOrDefaultAsync());
            var managerialDept = await _context.Departments.FirstOrDefaultAsync(d => d.Name == "Managerial" || d.Name == "Management");

            if (headOffice == null || hrDept == null)
            {
                ModelState.AddModelError("", "No branches or departments are configured in the system. Please configure them in Settings first.");
                return Page();
            }

            // --- Role-specific validation ---
            switch (SelectedRole)
            {
                case "HR Manager":
                {
                    var existingHrManagers = await _userManager.GetUsersInRoleAsync("HR Manager");
                    if (existingHrManagers.Any() || await _context.Users.AnyAsync(u => u.UserName == "hrmanager"))
                    {
                        ModelState.AddModelError("", "A Corporate HR Manager duty account already exists (hrmanager). Only one HR Manager is permitted for the organization.");
                    }
                    break;
                }

                case "Area Manager":
                {
                    if (string.IsNullOrWhiteSpace(AreaName))
                    {
                        ModelState.AddModelError("AreaName", "Please enter an area name (e.g. Central Province, Western Region).");
                    }
                    if (ManagedBranchIds == null || !ManagedBranchIds.Any())
                    {
                        ModelState.AddModelError("ManagedBranchIds", "Please select at least one branch for the Area Manager to oversee.");
                    }
                    if (string.IsNullOrWhiteSpace(AreaName) || ManagedBranchIds == null || !ManagedBranchIds.Any())
                    {
                        break;
                    }

                    // Area name must be unique
                    var proposedName = $"Area Manager - {AreaName.Trim()}";
                    var existingAreaManagers = await _userManager.GetUsersInRoleAsync("Area Manager");
                    if (existingAreaManagers.Any(u => u.FullName.Equals(proposedName, StringComparison.OrdinalIgnoreCase)))
                    {
                        ModelState.AddModelError("AreaName", "An Area Manager account for this area already exists.");
                    }

                    var takenBranchIds = new HashSet<int>();
                    foreach (var am in existingAreaManagers.Where(u => !string.IsNullOrEmpty(u.ManagedBranches)))
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
                        ModelState.AddModelError("DeptHeadBranchDeptId", "Invalid branch/department selection.");
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
                "HR Manager"      => "HR Manager",
                "Branch Manager"  => $"Branch Manager - {branchName}",
                "Area Manager"    => $"Area Manager - {AreaName.Trim()}",
                "Department Head" => $"Department Head - {deptHeadBD!.Department.Name} - {deptHeadBD!.Branch.Name}",
                _                 => AccountDisplayName
            };

            // Clamp display name to max 100 characters
            if (AccountDisplayName.Length > 100)
            {
                AccountDisplayName = AccountDisplayName.Substring(0, 100);
            }

            // --- Resolve branch / department / designation IDs ---
            var resolvedBranchId = SelectedRole switch
            {
                "HR Manager"      => headOffice.Id,
                "Area Manager"    => (ManagedBranchIds != null && ManagedBranchIds.Any()) ? ManagedBranchIds.First() : headOffice.Id,
                "Department Head" => deptHeadBD!.BranchId,
                _                 => BranchId!.Value
            };

            var resolvedDeptId = SelectedRole switch
            {
                "HR Manager"      => hrDept.Id,
                "Department Head" => deptHeadBD!.DepartmentId,
                _                 => (managerialDept?.Id ?? hrDept.Id)
            };

            var desigEntity = await _context.Designations.FirstOrDefaultAsync(d => d.Title == SelectedRole)
                              ?? await _context.Designations.FirstOrDefaultAsync();
            var resolvedDesigId = desigEntity?.Id ?? 0;

            var username = GenerateDutyUsername(SelectedRole, branchName, deptHeadBD);
            var email = GenerateDutyEmail(username);

            // Generate unique duty NIC and EPF
            var uniqueKey = SelectedRole switch
            {
                "HR Manager"      => "HRM",
                "Branch Manager"  => $"BM-{resolvedBranchId}",
                "Area Manager"    => $"AM-{Regex.Replace(AreaName.Trim().ToUpperInvariant(), @"[^A-Z0-9]", "")}",
                "Department Head" => $"DH-{deptHeadBD!.BranchId}-{deptHeadBD!.DepartmentId}",
                _                 => Guid.NewGuid().ToString("N").Substring(0, 6).ToUpperInvariant()
            };

            var dutyNic = $"DUTY-{uniqueKey}";
            var dutyEpf = $"DUTY-{uniqueKey}";

            // Ensure role exists in Identity
            if (!await _roleManager.RoleExistsAsync(SelectedRole))
            {
                await _roleManager.CreateAsync(new IdentityRole(SelectedRole));
            }

            // --- Wrap in transaction ---
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var employee = new Employee
                {
                    FullName           = AccountDisplayName,
                    Initials           = AccountDisplayName,
                    NIC                = dutyNic,
                    DateOfBirth        = new DateTime(1990, 1, 1),
                    Sex                = "N/A",
                    PhoneNumber        = "0000000000",
                    ResidentialAddress = "-",
                    EmployeeType       = "Permanent",
                    EPFNumber          = dutyEpf,
                    ETFNumber          = dutyEpf,
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
                    UserName           = username,
                    Email              = email,
                    EmailConfirmed     = true,
                    EmployeeId         = employee.Id,
                    FullName           = AccountDisplayName,
                    EpfNumber          = dutyEpf,
                    Branch             = SelectedRole == "Area Manager"
                                            ? AreaName.Trim()
                                            : SelectedRole == "HR Manager"
                                                ? headOffice.Name
                                                : SelectedRole == "Department Head"
                                                    ? deptHeadBD!.Branch.Name
                                                    : await GetBranchNameAsync(resolvedBranchId),
                    Department         = SelectedRole == "HR Manager"
                                            ? hrDept.Name
                                            : SelectedRole == "Department Head"
                                                ? deptHeadBD!.Department.Name
                                                : (managerialDept?.Name ?? "Managerial"),
                    Designation        = SelectedRole,
                    DateOfJoining      = DateTime.Now,
                    ManagedBranches    = SelectedRole == "Area Manager" && ManagedBranchIds != null
                                            ? string.Join(",", ManagedBranchIds)
                                            : null,
                    MustChangePassword = false
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

                var managedInfo = SelectedRole == "Area Manager" && ManagedBranchIds != null && ManagedBranchIds.Count > 0
                    ? $" ({ManagedBranchIds.Count} branches assigned)"
                    : string.Empty;
                var successMsg = $"Duty account '{AccountDisplayName}' ({SelectedRole}) created successfully.{managedInfo}";

                _context.Notifications.Add(new Notification
                {
                    UserId    = _userManager.GetUserId(User) ?? "",
                    Title     = "Duty Account Created",
                    Message   = $"{successMsg}\nUsername: {username}\nPassword: {password}",
                    TargetUrl = $"/Employees/Details/{employee.Id}",
                    IsRead    = false,
                    CreatedAt = HRMS.Domain.Common.SriLankaTime.Now,
                });
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                TempData["SuccessMessage"] = $"{successMsg} (Username: {username} | Password: {password})";
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                ModelState.AddModelError("", $"Failed to create duty account: {ex.Message}");
                return Page();
            }

            return RedirectToPage("./Index");
        }

        // ── Helpers ───────────────────────────────────────────────────────

        private async Task EnsureCoreDesignationsAsync()
        {
            string[] coreDesignations = ["Branch Manager", "Area Manager", "Department Head", "HR Manager"];
            foreach (var title in coreDesignations)
            {
                if (!await _context.Designations.AnyAsync(d => d.Title == title))
                {
                    _context.Designations.Add(new Designation { Title = title });
                }
            }
            await _context.SaveChangesAsync();
        }

        private async Task LoadDropdownsAsync()
        {
            // 0. Check HR Manager existence
            var hrManagerUsers = await _userManager.GetUsersInRoleAsync("HR Manager");
            HasExistingHrManager = hrManagerUsers.Any() || await _context.Users.AnyAsync(u => u.UserName == "hrmanager");

            // 1. All Branches
            var allBranches = await _context.Branches.OrderBy(b => b.Name).ToListAsync();
            BranchList = allBranches.Select(b => new SelectListItem
            {
                Value = b.Id.ToString(),
                Text  = string.IsNullOrEmpty(b.Location) ? b.Name : $"{b.Name} ({b.Location})"
            }).ToList();

            // 2. Branch Manager Branches: exclude branches that already have an active Branch Manager
            var existingBMUsers = await _userManager.GetUsersInRoleAsync("Branch Manager");
            var assignedBMEmpIds = existingBMUsers.Where(u => u.EmployeeId != null).Select(u => u.EmployeeId!.Value).ToList();
            var takenBranchIds = new HashSet<int>();
            if (assignedBMEmpIds.Count > 0)
            {
                takenBranchIds = (await _context.Employees
                    .Where(e => assignedBMEmpIds.Contains(e.Id))
                    .Select(e => e.BranchId)
                    .ToListAsync()).ToHashSet();
            }
            BranchManagerBranchList = allBranches
                .Where(b => !takenBranchIds.Contains(b.Id))
                .Select(b => new SelectListItem
                {
                    Value = b.Id.ToString(),
                    Text  = string.IsNullOrEmpty(b.Location) ? b.Name : $"{b.Name} ({b.Location})"
                }).ToList();

            // 3. Area Manager Branches: exclude branches already managed by an Area Manager
            var existingAMUsers = await _userManager.GetUsersInRoleAsync("Area Manager");
            var takenAMBranchIds = new HashSet<int>();
            foreach (var am in existingAMUsers.Where(u => !string.IsNullOrEmpty(u.ManagedBranches)))
            {
                foreach (var part in am.ManagedBranches!.Split(','))
                    if (int.TryParse(part, out var bid)) takenAMBranchIds.Add(bid);
            }
            AreaManagerBranchList = allBranches
                .Where(b => !takenAMBranchIds.Contains(b.Id))
                .Select(b => new SelectListItem
                {
                    Value = b.Id.ToString(),
                    Text  = string.IsNullOrEmpty(b.Location) ? b.Name : $"{b.Name} ({b.Location})"
                }).ToList();

            // 4. All Departments (excluding Managerial)
            var allDepartments = await _context.Departments
                .Where(d => d.Name != "Managerial" && d.Name != "Management")
                .OrderBy(d => d.Name)
                .ToListAsync();
            DepartmentList = allDepartments
                .Select(d => new SelectListItem { Value = d.Id.ToString(), Text = d.Name })
                .ToList();

            // 5. Department Head Branch-Department pairs
            var existingDHUsers = await _userManager.GetUsersInRoleAsync("Department Head");
            var assignedDHEmpIds = existingDHUsers.Where(u => u.EmployeeId != null).Select(u => u.EmployeeId!.Value).ToList();
            var takenDHPairs = new HashSet<(int BranchId, int DeptId)>();
            if (assignedDHEmpIds.Count > 0)
            {
                var takenList = await _context.Employees
                    .Where(e => assignedDHEmpIds.Contains(e.Id) && e.DepartmentId != null)
                    .Select(e => new { e.BranchId, DeptId = e.DepartmentId!.Value })
                    .ToListAsync();
                foreach (var item in takenList)
                    takenDHPairs.Add((item.BranchId, item.DeptId));
            }

            var allBranchDepts = await _context.BranchDepartments
                .Include(bd => bd.Branch)
                .Include(bd => bd.Department)
                .Where(bd => bd.Department.Name != "Managerial" && bd.Department.Name != "Management")
                .OrderBy(bd => bd.Branch.Name)
                .ThenBy(bd => bd.Department.Name)
                .ToListAsync();

            // If BranchDepartments has no entries yet, generate cross pairs from Branches x Departments
            if (allBranchDepts.Count == 0 && allBranches.Count > 0 && allDepartments.Count > 0)
            {
                foreach (var branch in allBranches)
                {
                    foreach (var dept in allDepartments.Where(d => d.Name != "Managerial" && d.Name != "Management"))
                    {
                        var newBD = new BranchDepartment { BranchId = branch.Id, DepartmentId = dept.Id };
                        _context.BranchDepartments.Add(newBD);
                    }
                }
                await _context.SaveChangesAsync();

                allBranchDepts = await _context.BranchDepartments
                    .Include(bd => bd.Branch)
                    .Include(bd => bd.Department)
                    .OrderBy(bd => bd.Branch.Name)
                    .ThenBy(bd => bd.Department.Name)
                    .ToListAsync();
            }

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

        private string GenerateDutyUsername(string role, string branchName, BranchDepartment? deptHeadBD)
        {
            return role switch
            {
                "HR Manager"      => "hrmanager",
                "Branch Manager"  => $"bm.{Regex.Replace(branchName.ToLowerInvariant(), @"[^a-z0-9]", "")}",
                "Area Manager"    => $"am.{Regex.Replace(AreaName.Trim().ToLowerInvariant(), @"[^a-z0-9]", "")}",
                "Department Head" => $"dh.{Regex.Replace((deptHeadBD?.Department?.Name ?? "dept").ToLowerInvariant(), @"[^a-z0-9]", "")}{Regex.Replace((deptHeadBD?.Branch?.Name ?? "branch").ToLowerInvariant(), @"[^a-z0-9]", "")}",
                _                 => Regex.Replace(role.ToLowerInvariant(), @"\s+", "")
            };
        }

        private string GenerateDutyEmail(string username)
        {
            return $"{username}@kanrich.lk";
        }

        private async Task<string> GetBranchNameAsync(int branchId)
        {
            var b = await _context.Branches.FindAsync(branchId);
            return b?.Name ?? $"Branch-{branchId}";
        }
    }
}
