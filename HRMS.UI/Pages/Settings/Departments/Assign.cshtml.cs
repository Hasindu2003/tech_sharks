using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using HRMS.Domain.Entities.Core;
using HRMS.Infrastructure.Identity;
using HRMS.Infrastructure.Persistence;

namespace HRMS.UI.Pages.Settings.Departments
{
    using Employee = HRMS.Domain.Entities.Core.Employee;

    [Authorize(Roles = "Admin")]

    public class AssignModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public AssignModel(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public Department Department { get; set; } = default!;
        public List<BranchCheckItem> Branches { get; set; } = new();
        public bool IsHumanResources { get; set; }
        public bool IsWelfare { get; set; }
        public bool IsCorporateHeadOfficeOnly => IsHumanResources || IsWelfare;

        [BindProperty]
        public int DepartmentId { get; set; }

        [BindProperty]
        public List<int> SelectedBranchIds { get; set; } = new();

        public class BranchCheckItem
        {
            public int Id { get; set; }
            public string Name { get; set; } = "";
            public string Location { get; set; } = "";
            public bool IsAssigned { get; set; }
        }

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null) return NotFound();

            Department = await _context.Departments
                .Include(d => d.BranchDepartments)
                .FirstOrDefaultAsync(d => d.Id == id) ?? default!;

            if (Department == null) return NotFound();

            DepartmentId = Department.Id;
            IsHumanResources = Department.Name.Equals("Human Resources", StringComparison.OrdinalIgnoreCase) ||
                               Department.Name.Equals("HR", StringComparison.OrdinalIgnoreCase);
            IsWelfare = Department.Name.Equals("Welfare", StringComparison.OrdinalIgnoreCase);

            await LoadBranchesAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var dept = await _context.Departments
                .Include(d => d.BranchDepartments)
                .FirstOrDefaultAsync(d => d.Id == DepartmentId);

            if (dept == null) return NotFound();

            var isHr = dept.Name.Equals("Human Resources", StringComparison.OrdinalIgnoreCase) ||
                       dept.Name.Equals("HR", StringComparison.OrdinalIgnoreCase);
            var isWelfare = dept.Name.Equals("Welfare", StringComparison.OrdinalIgnoreCase);
            var isCorporate = isHr || isWelfare;

            var headOffice = await _context.Branches.FirstOrDefaultAsync(b => b.Name == "Head Office" || b.Name == "Head Office - Colombo" || b.Name.Contains("Head Office"))
                             ?? await _context.Branches.FirstOrDefaultAsync();

            if (isCorporate)
            {
                // Strictly enforce Head Office only for Corporate departments (HR & Welfare)
                SelectedBranchIds = headOffice != null ? new List<int> { headOffice.Id } : new List<int>();
            }

            // Detect newly added branch assignments (before we remove them)
            var previousBranchIds = dept.BranchDepartments.Select(bd => bd.BranchId).ToHashSet();
            var newlyAddedBranchIds = SelectedBranchIds.Where(id => !previousBranchIds.Contains(id)).ToList();

            // Update branch-department assignments
            _context.BranchDepartments.RemoveRange(dept.BranchDepartments);
            foreach (var branchId in SelectedBranchIds)
            {
                _context.BranchDepartments.Add(new BranchDepartment
                {
                    BranchId     = branchId,
                    DepartmentId = DepartmentId
                });
            }
            await _context.SaveChangesAsync();

            // Auto-create a Department Head account for each newly added branch combo (except HR, which is led by HR Manager)
            var accountsCreated = new List<string>();

            if (!isHr && newlyAddedBranchIds.Count > 0)
            {
                // Find (BranchId, DepartmentId) pairs that already have a Dept Head
                var dhEmpIds = (await _userManager.GetUsersInRoleAsync("Department Head"))
                    .Where(u => u.EmployeeId != null).Select(u => u.EmployeeId!.Value).ToList();

                var takenPairs = new HashSet<(int BranchId, int DeptId)>();
                if (dhEmpIds.Count > 0)
                {
                    var pairs = await _context.Employees
                        .Where(e => dhEmpIds.Contains(e.Id) && e.DepartmentId != null)
                        .Select(e => new { e.BranchId, DeptId = e.DepartmentId!.Value })
                        .ToListAsync();
                    foreach (var p in pairs)
                        takenPairs.Add((p.BranchId, p.DeptId));
                }

                foreach (var branchId in newlyAddedBranchIds)
                {
                    if (takenPairs.Contains((branchId, DepartmentId)))
                        continue; // Account already exists for this combo — skip

                    var branch = await _context.Branches.FindAsync(branchId);
                    if (branch == null) continue;

                    var displayName = $"Department Head - {dept.Name} - {branch.Name}";
                    var deptSlug   = Regex.Replace(dept.Name.ToLowerInvariant(), @"[^a-z0-9]", "");
                    var branchSlug = Regex.Replace(branch.Name.ToLowerInvariant(), @"[^a-z0-9]", "");
                    var username   = $"dh.{deptSlug}{branchSlug}";
                    var email      = $"{username}@kanrich.lk";
                    var password   = $"Kanrich@{new Random().Next(1000, 9999)}";
                    var desigId    = await GetFallbackDesignationIdAsync(DepartmentId);

                    await using var tx = await _context.Database.BeginTransactionAsync();
                    try
                    {
                        var employee = new Employee
                        {
                            FullName           = displayName,
                            Initials           = displayName,
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
                            BranchId           = branchId,
                            DepartmentId       = DepartmentId,
                            DesignationId      = desigId > 0 ? desigId : null,
                        };
                        _context.Employees.Add(employee);
                        await _context.SaveChangesAsync();

                        var user = new ApplicationUser
                        {
                            UserName       = username,
                            Email          = email,
                            EmailConfirmed = true,
                            EmployeeId     = employee.Id,
                            FullName       = displayName,
                            EpfNumber      = "N/A",
                            Branch         = branch.Name,
                            Department     = dept.Name,
                            Designation    = "Department Head",
                            DateOfJoining  = DateTime.Now,
                        };

                        var result = await _userManager.CreateAsync(user, password);
                        if (!result.Succeeded)
                        {
                            await tx.RollbackAsync();
                            continue;
                        }

                        await _userManager.AddToRoleAsync(user, "Department Head");

                        _context.Notifications.Add(new Notification
                        {
                            UserId    = _userManager.GetUserId(User) ?? "",
                            Title     = "Dept Head Account Auto-Created",
                            Message   = $"Department Head account created for {dept.Name} — {branch.Name}.\nUsername: {username}\nPassword: {password}",
                            TargetUrl = $"/Employees/Details/{employee.Id}",
                            IsRead    = false,
                            CreatedAt = HRMS.Domain.Common.SriLankaTime.Now,
                        });
                        await _context.SaveChangesAsync();
                        await tx.CommitAsync();

                        accountsCreated.Add($"{dept.Name} — {branch.Name} ({username})");
                    }
                    catch
                    {
                        await tx.RollbackAsync();
                    }
                }
            }

            var msg = $"Branch assignments updated for '{dept.Name}'.";
            if (accountsCreated.Count > 0)
                msg += $" Department Head account(s) auto-created for: {string.Join(", ", accountsCreated)}.";

            TempData["SuccessMessage"] = msg;
            return RedirectToPage("./Index");
        }

        private async Task LoadBranchesAsync()
        {
            var isCorporate = Department.Name.Equals("Human Resources", StringComparison.OrdinalIgnoreCase) ||
                              Department.Name.Equals("HR", StringComparison.OrdinalIgnoreCase) ||
                              Department.Name.Equals("Welfare", StringComparison.OrdinalIgnoreCase);
            var assignedIds = Department.BranchDepartments.Select(bd => bd.BranchId).ToHashSet();
            
            var branchesQuery = _context.Branches.AsQueryable();
            if (isCorporate)
            {
                branchesQuery = branchesQuery.Where(b => b.Name == "Head Office" || b.Name == "Head Office - Colombo" || b.Name.Contains("Head Office"));
            }

            var branches = await branchesQuery.OrderBy(b => b.Name).ToListAsync();
            Branches = branches.Select(b => new BranchCheckItem
            {
                Id         = b.Id,
                Name       = b.Name,
                Location   = b.Location,
                IsAssigned = isCorporate || assignedIds.Contains(b.Id)
            }).ToList();
        }

        private async Task<int> GetFallbackDesignationIdAsync(int departmentId)
        {
            var dd = await _context.DepartmentDesignations
                .Where(dd => dd.DepartmentId == departmentId)
                .OrderBy(dd => dd.Id)
                .FirstOrDefaultAsync();
            return dd?.DesignationId ?? 0;
        }
    }
}
