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

namespace HRMS.UI.Pages.HRManager
{
    [Authorize(Roles = "HR Manager")]
    public class AssignBranchesModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;

        public AssignBranchesModel(UserManager<ApplicationUser> userManager, ApplicationDbContext context)
        {
            _userManager = userManager;
            _context = context;
        }

        public List<HROfficerDto> HROfficers { get; set; } = new();
        public List<Branch> AllBranches { get; set; } = new();

        [BindProperty]
        public string TargetUserId { get; set; } = string.Empty;

        [BindProperty]
        public List<int> SelectedBranchIds { get; set; } = new();

        public async Task OnGetAsync()
        {
            await LoadDataAsync();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (string.IsNullOrEmpty(TargetUserId))
            {
                TempData["ErrorMessage"] = "Invalid officer selected.";
                return RedirectToPage();
            }

            var user = await _userManager.FindByIdAsync(TargetUserId);
            if (user == null)
            {
                TempData["ErrorMessage"] = "User not found.";
                return RedirectToPage();
            }

            user.ManagedBranches = SelectedBranchIds.Any()
                ? string.Join(",", SelectedBranchIds)
                : null;

            await _userManager.UpdateAsync(user);

            var branchCount = SelectedBranchIds.Count;
            TempData["SuccessMessage"] = branchCount > 0
                ? $"Updated branch assignments for '{user.FullName}' ({branchCount} branches assigned)."
                : $"Updated '{user.FullName}' to have unrestricted access across all branches.";

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostCreateOfficerAsync(string newOfficerName, List<int> newOfficerBranchIds)
        {
            if (string.IsNullOrWhiteSpace(newOfficerName))
            {
                TempData["ErrorMessage"] = "Please provide an officer name or identifier.";
                return RedirectToPage();
            }

            var cleanName = Regex.Replace(newOfficerName.Trim().ToLowerInvariant(), @"[^a-z0-9]", "");
            if (string.IsNullOrEmpty(cleanName)) cleanName = "officer";

            var username = $"hro.{cleanName}";
            var email = $"{username}@kanrich.lk";
            var displayName = $"HR Officer - {newOfficerName.Trim()}";

            if (await _userManager.FindByNameAsync(username) != null || await _userManager.FindByEmailAsync(email) != null)
            {
                TempData["ErrorMessage"] = $"An HR Officer account with username '{username}' already exists.";
                return RedirectToPage();
            }

            var headOffice = await _context.Branches.FirstOrDefaultAsync(b => b.Name == "Head Office" || b.Name == "Head Office - Colombo")
                             ?? await _context.Branches.FirstOrDefaultAsync();
            var hrDept = await _context.Departments.FirstOrDefaultAsync(d => d.Name == "Human Resources" || d.Name == "HR")
                          ?? await _context.Departments.FirstOrDefaultAsync();

            if (headOffice == null || hrDept == null)
            {
                TempData["ErrorMessage"] = "Head Office or Human Resources department not configured.";
                return RedirectToPage();
            }

            var desig = await _context.Designations.FirstOrDefaultAsync(d => d.Title == "HR Officer")
                         ?? await _context.Designations.FirstOrDefaultAsync();

            await using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                var employee = new HRMS.Domain.Entities.Core.Employee
                {
                    FullName           = displayName,
                    Initials           = displayName,
                    NIC                = "DUTY-ACC",
                    DateOfBirth        = new DateTime(1990, 1, 1),
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
                    BranchId           = headOffice.Id,
                    DepartmentId       = hrDept.Id,
                    DesignationId      = desig?.Id
                };
                _context.Employees.Add(employee);
                await _context.SaveChangesAsync();

                var password = $"Kanrich@{new Random().Next(1000, 9999)}";
                var user = new ApplicationUser
                {
                    UserName        = username,
                    Email           = email,
                    EmailConfirmed  = true,
                    EmployeeId      = employee.Id,
                    FullName        = displayName,
                    EpfNumber       = "N/A",
                    Branch          = headOffice.Name,
                    Department      = hrDept.Name,
                    Designation     = "HR Officer",
                    DateOfJoining   = DateTime.Now,
                    ManagedBranches = newOfficerBranchIds != null && newOfficerBranchIds.Any()
                                        ? string.Join(",", newOfficerBranchIds)
                                        : null
                };

                var result = await _userManager.CreateAsync(user, password);
                if (!result.Succeeded)
                {
                    await tx.RollbackAsync();
                    TempData["ErrorMessage"] = string.Join(", ", result.Errors.Select(e => e.Description));
                    return RedirectToPage();
                }

                await _userManager.AddToRoleAsync(user, "HR Officer");

                _context.Notifications.Add(new Notification
                {
                    UserId    = _userManager.GetUserId(User) ?? "",
                    Title     = "HR Officer Account Created",
                    Message   = $"HR Officer '{displayName}' created successfully.\nUsername: {username}\nPassword: {password}",
                    TargetUrl = $"/Employees/Details/{employee.Id}",
                    IsRead    = false,
                    CreatedAt = HRMS.Domain.Common.SriLankaTime.Now
                });
                await _context.SaveChangesAsync();
                await tx.CommitAsync();

                TempData["SuccessMessage"] = $"HR Officer '{displayName}' created successfully! (Username: {username} | Password: {password})";
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                TempData["ErrorMessage"] = $"Error creating HR Officer: {ex.Message}";
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDeleteOfficerAsync(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound();

            var displayName = user.FullName;
            var empId = user.EmployeeId;

            await using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                // 1. Delete user role links and user row safely
                await _context.Database.ExecuteSqlRawAsync(@"
                    DELETE FROM AspNetUserRoles WHERE UserId = {0};
                    IF OBJECT_ID('dbo.AspNetUserClaims', 'U') IS NOT NULL DELETE FROM AspNetUserClaims WHERE UserId = {0};
                    IF OBJECT_ID('dbo.AspNetUserTokens', 'U') IS NOT NULL DELETE FROM AspNetUserTokens WHERE UserId = {0};
                    IF OBJECT_ID('dbo.AspNetUserLogins', 'U') IS NOT NULL DELETE FROM AspNetUserLogins WHERE UserId = {0};
                    DELETE FROM AspNetUsers WHERE Id = {0};
                ", user.Id);

                // 2. Remove linked employee if any
                if (empId.HasValue)
                {
                    var emp = await _context.Employees.FindAsync(empId.Value);
                    if (emp != null)
                    {
                        _context.Employees.Remove(emp);
                        await _context.SaveChangesAsync();
                    }
                }

                await tx.CommitAsync();
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                TempData["ErrorMessage"] = $"Failed to delete officer: {ex.Message}";
                return RedirectToPage();
            }

            TempData["SuccessMessage"] = $"HR Officer '{displayName}' deleted successfully.";
            return RedirectToPage();
        }

        private async Task LoadDataAsync()
        {
            AllBranches = await _context.Branches.OrderBy(b => b.Name).ToListAsync();
            var officers = await _userManager.GetUsersInRoleAsync("HR Officer");

            HROfficers = new List<HROfficerDto>();
            foreach (var off in officers)
            {
                var assignedIds = new List<int>();
                if (!string.IsNullOrEmpty(off.ManagedBranches))
                {
                    assignedIds = off.ManagedBranches.Split(',')
                        .Select(s => int.TryParse(s, out var id) ? id : 0)
                        .Where(id => id > 0).ToList();
                }

                var assignedNames = AllBranches
                    .Where(b => assignedIds.Contains(b.Id))
                    .Select(b => b.Name).ToList();

                HROfficers.Add(new HROfficerDto
                {
                    UserId              = off.Id,
                    UserName            = off.UserName ?? string.Empty,
                    FullName            = off.FullName,
                    Email               = off.Email ?? string.Empty,
                    AssignedBranchIds   = assignedIds,
                    AssignedBranchNames = assignedNames.Any() ? string.Join(", ", assignedNames) : "All Branches (Global Access)"
                });
            }

            HROfficers = HROfficers.OrderBy(o => o.FullName).ToList();
        }

        public class HROfficerDto
        {
            public string UserId              { get; set; } = string.Empty;
            public string UserName            { get; set; } = string.Empty;
            public string FullName            { get; set; } = string.Empty;
            public string Email               { get; set; } = string.Empty;
            public List<int> AssignedBranchIds { get; set; } = new();
            public string AssignedBranchNames { get; set; } = string.Empty;
        }
    }
}
