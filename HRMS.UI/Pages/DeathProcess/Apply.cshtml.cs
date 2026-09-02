using HRMS.Application.Models;
using HRMS.Application.Services;
using HRMS.Infrastructure.Identity;
using HRMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace HRMS.UI.Pages.DeathProcess
{
    [Authorize(Roles = "Branch Manager")]
    public class ApplyModel : PageModel
    {
        private readonly IDeathService _deathService;
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public ApplyModel(IDeathService deathService, ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _deathService = deathService;
            _context = context;
            _userManager = userManager;
        }

        [BindProperty]
        public DeathRequestViewModel RequestModel { get; set; } = new();

        public List<EmployeeOptionDto> Employees { get; set; } = new();
        public List<string> Branches { get; set; } = new();

        public class EmployeeOptionDto
        {
            public int Id { get; set; }
            public string FullName { get; set; } = string.Empty;
            public string EpfNumber { get; set; } = string.Empty;
            public string Email { get; set; } = string.Empty;
            public string Branch { get; set; } = string.Empty;
            public string Department { get; set; } = string.Empty;
            public string Designation { get; set; } = string.Empty;
        }

        public async Task OnGetAsync(string? employeeName, string? epfNumber, string? email, string? branch, string? dept, string? designation)
        {
            await PopulateEmployeesAsync();

            Branches = await _context.Branches.Select(b => b.Name).OrderBy(n => n).ToListAsync();

            RequestModel.EmployeeName = employeeName ?? "";
            RequestModel.EpfNumber = epfNumber ?? "";
            RequestModel.EmployeeEmail = email ?? "";
            RequestModel.Branch = branch ?? "";
            RequestModel.Department = dept ?? "";
            RequestModel.Designation = designation ?? "";
            RequestModel.DateOfDeath = DateTime.Today;
        }

        public async Task<IActionResult> OnPostAsync(List<IFormFile> documents)
        {
            await PopulateEmployeesAsync();

            if (documents == null || documents.Count == 0)
            {
                ModelState.AddModelError("documents", "At least one mandatory document (e.g., Death Certificate) must be uploaded.");
            }

            if (!ModelState.IsValid)
            {
                return Page();
            }

            var user = await _userManager.GetUserAsync(User);
            var initiatedByEmail = user?.Email ?? User.FindFirstValue(ClaimTypes.Email) ?? User.Identity?.Name ?? "Manager";
            
            try
            {
                var id = await _deathService.SubmitRequestAsync(RequestModel, documents!, initiatedByEmail);
                TempData["SuccessMessage"] = $"Death Process for {RequestModel.EmployeeName} (EPF: {RequestModel.EpfNumber}) initiated successfully and forwarded to Area Manager review.";
                return RedirectToPage("/Separation/Dashboard", new { ActiveTab = "Death" });
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "An error occurred: " + ex.Message);
                return Page();
            }
        }

        private async Task PopulateEmployeesAsync()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            var isBranchManager = User.IsInRole("Branch Manager");
            var userBranch = currentUser?.Branch ?? string.Empty;

            var (dutyEmployeeIds, dutyIdentifiers) = await GetDutyAccountExclusionsAsync();

            var query = _context.Employees
                .Include(e => e.Branch)
                .Include(e => e.Department)
                .Include(e => e.Designation)
                .Where(e => e.NIC != "DUTY-ACC" 
                         && e.Status != "Draft" 
                         && e.Status != "Terminated" 
                         && e.Status != "Resigned"
                         && e.Status != "Deceased");

            if (isBranchManager && !string.IsNullOrWhiteSpace(userBranch))
            {
                var ub = userBranch.Trim().ToLower();
                query = query.Where(e => e.Branch != null && e.Branch.Name.ToLower() == ub);
            }

            var dbEmployees = await query.ToListAsync();

            Employees = dbEmployees
                .Where(e => !dutyEmployeeIds.Contains(e.Id)
                         && (string.IsNullOrEmpty(e.Email) || !dutyIdentifiers.Contains(e.Email.Trim()))
                         && (string.IsNullOrEmpty(e.EPFNumber) || !dutyIdentifiers.Contains(e.EPFNumber.Trim())))
                .OrderBy(e => e.FullName)
                .Select(e => new EmployeeOptionDto
                {
                    Id = e.Id,
                    FullName = e.FullName?.Trim() ?? string.Empty,
                    EpfNumber = e.EPFNumber ?? string.Empty,
                    Email = e.Email ?? string.Empty,
                    Branch = e.Branch?.Name ?? string.Empty,
                    Department = e.Department?.Name ?? string.Empty,
                    Designation = e.Designation?.Title ?? string.Empty
                })
                .ToList();
        }

        private async Task<(HashSet<int> dutyEmployeeIds, HashSet<string> dutyIdentifiers)> GetDutyAccountExclusionsAsync()
        {
            var dutyRoles = new[] { "Admin", "HR Manager", "HR Officer", "Branch Manager", "Area Manager", "Department Head" };
            var dutyUserIds = new HashSet<string>();

            foreach (var role in dutyRoles)
            {
                var usersInRole = await _userManager.GetUsersInRoleAsync(role);
                foreach (var u in usersInRole)
                {
                    dutyUserIds.Add(u.Id);
                }
            }

            var dutyUsers = await _userManager.Users
                .Where(u => dutyUserIds.Contains(u.Id))
                .ToListAsync();

            var dutyIdentifiers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var dutyEmployeeIds = new HashSet<int>();

            foreach (var u in dutyUsers)
            {
                if (u.EmployeeId.HasValue) dutyEmployeeIds.Add(u.EmployeeId.Value);
                if (!string.IsNullOrEmpty(u.Email)) dutyIdentifiers.Add(u.Email.Trim());
                if (!string.IsNullOrEmpty(u.UserName)) dutyIdentifiers.Add(u.UserName.Trim());
                if (!string.IsNullOrEmpty(u.EpfNumber)) dutyIdentifiers.Add(u.EpfNumber.Trim());
            }

            return (dutyEmployeeIds, dutyIdentifiers);
        }
    }
}
