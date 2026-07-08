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
            await LoadBranchesAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var dept = await _context.Departments
                .Include(d => d.BranchDepartments)
                .FirstOrDefaultAsync(d => d.Id == DepartmentId);

            if (dept == null) return NotFound();

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

            // Auto-create a Department Head account for each newly added branch combo
            var accountsCreated = new List<string>();

            if (newlyAddedBranchIds.Count > 0)
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
                    var email      = GenerateDutyEmail(displayName);
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
                            UserName       = email,
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
                            Message   = $"Department Head account created for {dept.Name} — {branch.Name}.\nEmail: {email}\nPassword: {password}",
                            TargetUrl = $"/Employees/Details/{employee.Id}",
                            IsRead    = false,
                            CreatedAt = DateTime.Now,
                        });
                        await _context.SaveChangesAsync();
                        await tx.CommitAsync();

                        accountsCreated.Add($"{dept.Name} — {branch.Name}");
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
            var assignedIds = Department.BranchDepartments.Select(bd => bd.BranchId).ToHashSet();
            var branches    = await _context.Branches.OrderBy(b => b.Name).ToListAsync();
            Branches = branches.Select(b => new BranchCheckItem
            {
                Id         = b.Id,
                Name       = b.Name,
                Location   = b.Location,
                IsAssigned = assignedIds.Contains(b.Id)
            }).ToList();
        }

        private string GenerateDutyEmail(string displayName)
        {
            const string roleSlug = "departmenthead";

            // Extract the part after the first " - " separator
            var namePart = displayName;
            var idx = displayName.IndexOf(" - ", StringComparison.Ordinal);
            if (idx >= 0)
                namePart = displayName[(idx + 3)..].Trim();

            var nameSlug = Regex.Replace(namePart.ToLowerInvariant(), @"[^a-z0-9]", "");
            if (string.IsNullOrEmpty(nameSlug))
                nameSlug = Regex.Replace(displayName.ToLowerInvariant(), @"[^a-z0-9]", "");

            var baseEmail = $"{roleSlug}.{nameSlug}@kanrich.lk";
            var counter   = 1;
            var email     = baseEmail;
            while (_context.Employees.Any(e => e.Email == email))
            {
                email = $"{roleSlug}.{nameSlug}{counter++}@kanrich.lk";
            }
            return email;
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
