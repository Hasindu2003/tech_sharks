using HRMS.Application.Models;
using HRMS.Application.Services;
using HRMS.Infrastructure.Identity;
using HRMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace HRMS.UI.Pages.HRManager
{
    [Authorize(Roles = "HR Manager,HR Officer")]
    public class InitiateTransferModel : PageModel
    {
        private readonly ITransferRequestService _transferService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;

        public InitiateTransferModel(
            ITransferRequestService transferService,
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext context)
        {
            _transferService = transferService;
            _userManager = userManager;
            _context = context;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public List<SelectListItem> AssignedBranchList { get; set; } = new();
        public List<SelectListItem> Employees { get; set; } = new();
        public List<string> AllBranches { get; set; } = new();

        public class InputModel
        {
            [Required(ErrorMessage = "Please select the employee's current branch.")]
            [Display(Name = "Current Branch")]
            public int? SelectedBranchId { get; set; }

            [Required(ErrorMessage = "Please select an employee.")]
            [Range(1, int.MaxValue, ErrorMessage = "Please select an employee.")]
            [Display(Name = "Employee")]
            public int? SelectedEmployeeId { get; set; }

            [Required(ErrorMessage = "Please select the branch you want to transfer to.")]
            [Display(Name = "Requested Branch")]
            public string RequestedBranch { get; set; } = string.Empty;

            [Required(ErrorMessage = "Preferred transfer date is required.")]
            [DataType(DataType.Date)]
            [Display(Name = "Preferred Transfer Date")]
            public DateTime? PreferredDate { get; set; }

            [Required(ErrorMessage = "Reason for transfer is required.")]
            [StringLength(500, MinimumLength = 20,
                ErrorMessage = "Reason must be between 20 and 500 characters.")]
            [Display(Name = "Reason for Transfer")]
            public string Reason { get; set; } = string.Empty;

            [Display(Name = "Supporting Document")]
            public IFormFile? Document { get; set; }
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var hrUser = await GetCurrentUserAsync();
            if (hrUser == null) return Challenge();

            await LoadAssignedBranchesAndDataAsync(hrUser, Input.SelectedBranchId);
            return Page();
        }

        public async Task<IActionResult> OnGetEmployeesByBranchAsync(int branchId)
        {
            var hrUser = await GetCurrentUserAsync();
            if (hrUser == null) return Unauthorized();

            var assignedIds = await GetAssignedBranchIdsAsync(hrUser);
            if (assignedIds.Any() && !assignedIds.Contains(branchId))
            {
                return Forbid();
            }

            var (dutyEmployeeIds, dutyIdentifiers) = await GetDutyAccountExclusionsAsync();

            var rawEmployees = await _context.Employees
                .Include(e => e.Designation)
                .Include(e => e.Department)
                .Include(e => e.Branch)
                .Where(e => e.BranchId == branchId 
                         && !e.NIC.StartsWith("DUTY")
                         && e.NIC != "DUTY-ACC" 
                         && e.Status != "Draft" 
                         && e.Status != "Terminated" 
                         && e.Status != "Resigned")
                .OrderBy(e => e.FullName)
                .ToListAsync();

            var users = await _context.Users
                .Where(u => !string.IsNullOrEmpty(u.Designation) || !string.IsNullOrEmpty(u.Department))
                .Select(u => new { u.EmployeeId, u.Email, u.UserName, u.EpfNumber, u.Designation, u.Department })
                .ToListAsync();

            var userByEmpId = users.Where(u => u.EmployeeId.HasValue).ToDictionary(u => u.EmployeeId!.Value, u => u);
            var userByEmail = users.Where(u => !string.IsNullOrEmpty(u.Email)).GroupBy(u => u.Email!, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
            var userByEpf = users.Where(u => !string.IsNullOrEmpty(u.EpfNumber)).GroupBy(u => u.EpfNumber!, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            var filteredEmployees = rawEmployees
                .Where(e => !dutyEmployeeIds.Contains(e.Id)
                         && (string.IsNullOrEmpty(e.Email) || !dutyIdentifiers.Contains(e.Email.Trim()))
                         && (string.IsNullOrEmpty(e.EPFNumber) || !dutyIdentifiers.Contains(e.EPFNumber.Trim())))
                .Select(e =>
                {
                    string desig = e.Designation?.Title ?? "";
                    if (string.IsNullOrWhiteSpace(desig))
                    {
                        if (userByEmpId.TryGetValue(e.Id, out var u) && !string.IsNullOrWhiteSpace(u.Designation))
                            desig = u.Designation;
                        else if (!string.IsNullOrEmpty(e.Email) && userByEmail.TryGetValue(e.Email, out u) && !string.IsNullOrWhiteSpace(u.Designation))
                            desig = u.Designation;
                        else if (!string.IsNullOrEmpty(e.EPFNumber) && userByEpf.TryGetValue(e.EPFNumber, out u) && !string.IsNullOrWhiteSpace(u.Designation))
                            desig = u.Designation;
                    }

                    string dept = e.Department?.Name ?? "";
                    if (string.IsNullOrWhiteSpace(dept))
                    {
                        if (userByEmpId.TryGetValue(e.Id, out var u) && !string.IsNullOrWhiteSpace(u.Department))
                            dept = u.Department;
                        else if (!string.IsNullOrEmpty(e.Email) && userByEmail.TryGetValue(e.Email, out u) && !string.IsNullOrWhiteSpace(u.Department))
                            dept = u.Department;
                        else if (!string.IsNullOrEmpty(e.EPFNumber) && userByEpf.TryGetValue(e.EPFNumber, out u) && !string.IsNullOrWhiteSpace(u.Department))
                            dept = u.Department;
                    }

                    return new
                    {
                        id = e.Id,
                        fullName = e.FullName,
                        epfNumber = !string.IsNullOrWhiteSpace(e.EPFNumber) ? e.EPFNumber : "N/A",
                        email = e.Email ?? "",
                        designationTitle = desig,
                        departmentName = !string.IsNullOrWhiteSpace(dept) ? dept : "General",
                        branchName = e.Branch != null ? e.Branch.Name : "N/A",
                        dateJoined = e.DateJoined.HasValue ? e.DateJoined.Value.ToString("yyyy-MM-dd") : null,
                        yearsOfService = e.DateJoined.HasValue ? (int)((DateTime.Today - e.DateJoined.Value).TotalDays / 365.25) : 0
                    };
                })
                .ToList();

            return new JsonResult(filteredEmployees);
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var hrUser = await GetCurrentUserAsync();
            if (hrUser == null) return Challenge();

            if (Input.PreferredDate.HasValue)
            {
                var minDate = DateTime.Today.AddDays(7);
                var maxDate = DateTime.Today.AddYears(1);

                if (Input.PreferredDate.Value.Date < minDate)
                {
                    ModelState.AddModelError("Input.PreferredDate",
                        "Preferred date must be at least 7 days from today.");
                }
                else if (Input.PreferredDate.Value.Date > maxDate)
                {
                    ModelState.AddModelError("Input.PreferredDate",
                        "Preferred date cannot be more than 1 year from today.");
                }
            }

            if (Input.Document != null && Input.Document.Length > 5 * 1024 * 1024)
            {
                ModelState.AddModelError("Input.Document", "File size must not exceed 5 MB.");
            }

            if (Input.Document != null)
            {
                var allowedExtensions = new[] { ".pdf", ".doc", ".docx", ".jpg", ".jpeg", ".png" };
                var extension = Path.GetExtension(Input.Document.FileName).ToLowerInvariant();
                if (!allowedExtensions.Contains(extension))
                {
                    ModelState.AddModelError("Input.Document",
                        "Only PDF, DOC, DOCX, JPG, and PNG files are allowed.");
                }
            }

            if (!ModelState.IsValid)
            {
                await LoadAssignedBranchesAndDataAsync(hrUser, Input.SelectedBranchId);
                return Page();
            }

            var targetEmployee = await _context.Employees
                .Include(e => e.Designation)
                .Include(e => e.Department)
                .Include(e => e.Branch)
                .Where(e => e.Id == Input.SelectedEmployeeId 
                         && !e.NIC.StartsWith("DUTY")
                         && e.NIC != "DUTY-ACC" 
                         && e.Status != "Draft" 
                         && e.Status != "Terminated" 
                         && e.Status != "Resigned")
                .Select(e => new
                {
                    e.Id,
                    e.FullName,
                    e.EPFNumber,
                    e.Email,
                    e.DateJoined,
                    e.BranchId,
                    e.DepartmentId,
                    e.DesignationId,
                    BranchName = e.Branch != null ? e.Branch.Name : "",
                    DesignationTitle = e.Designation != null ? e.Designation.Title : "",
                    DepartmentName = e.Department != null ? e.Department.Name : ""
                })
                .FirstOrDefaultAsync();

            if (targetEmployee == null)
            {
                ModelState.AddModelError("Input.SelectedEmployeeId", "Invalid employee selected.");
                await LoadAssignedBranchesAndDataAsync(hrUser, Input.SelectedBranchId);
                return Page();
            }

            var assignedIds = await GetAssignedBranchIdsAsync(hrUser);
            if (assignedIds.Any() && !assignedIds.Contains(targetEmployee.BranchId))
            {
                ModelState.AddModelError("Input.SelectedEmployeeId",
                    "You can only initiate transfers for employees in your assigned branches.");
                await LoadAssignedBranchesAndDataAsync(hrUser, Input.SelectedBranchId);
                return Page();
            }

            if (string.Equals(Input.RequestedBranch, targetEmployee.BranchName, StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError("Input.RequestedBranch",
                    $"The employee is already at {targetEmployee.BranchName}. You cannot request a transfer to their current branch.");
                await LoadAssignedBranchesAndDataAsync(hrUser, Input.SelectedBranchId);
                return Page();
            }

            var joiningDate = targetEmployee.DateJoined ?? DateTime.Today;
            var yearsOfService = (int)((DateTime.Today - joiningDate).TotalDays / 365.25);

            byte[]? documentData = null;
            string? documentFileName = null;
            string? documentContentType = null;

            if (Input.Document != null)
            {
                using var memoryStream = new MemoryStream();
                await Input.Document.CopyToAsync(memoryStream);
                documentData = memoryStream.ToArray();
                documentFileName = Input.Document.FileName;
                documentContentType = Input.Document.ContentType;
            }

            var roleName = User.IsInRole("HR Officer") ? "HR Officer" : "HR Manager";

            string desigTitle = targetEmployee.DesignationTitle;
            string deptTitle = targetEmployee.DepartmentName;

            if (string.IsNullOrWhiteSpace(desigTitle) || string.IsNullOrWhiteSpace(deptTitle))
            {
                if (targetEmployee.DepartmentId.HasValue && targetEmployee.DepartmentId.Value > 0 && string.IsNullOrWhiteSpace(deptTitle))
                {
                    var d = await _context.Departments.FindAsync(targetEmployee.DepartmentId.Value);
                    if (d != null) deptTitle = d.Name;
                }

                if (targetEmployee.DesignationId.HasValue && targetEmployee.DesignationId.Value > 0 && string.IsNullOrWhiteSpace(desigTitle))
                {
                    var des = await _context.Designations.FindAsync(targetEmployee.DesignationId.Value);
                    if (des != null) desigTitle = des.Title;
                }

                var matchingUser = await _context.Users
                    .FirstOrDefaultAsync(u => (u.EmployeeId == targetEmployee.Id) ||
                                              (!string.IsNullOrEmpty(targetEmployee.Email) && u.Email == targetEmployee.Email) ||
                                              (!string.IsNullOrEmpty(targetEmployee.EPFNumber) && u.EpfNumber == targetEmployee.EPFNumber));
                if (matchingUser != null)
                {
                    if (string.IsNullOrWhiteSpace(desigTitle) && !string.IsNullOrWhiteSpace(matchingUser.Designation))
                        desigTitle = matchingUser.Designation;
                    if (string.IsNullOrWhiteSpace(deptTitle) && !string.IsNullOrWhiteSpace(matchingUser.Department))
                        deptTitle = matchingUser.Department;
                }
            }

            var request = new TransferRequestViewModel
            {
                EmployeeName = targetEmployee.FullName,
                EpfNumber = !string.IsNullOrWhiteSpace(targetEmployee.EPFNumber) ? targetEmployee.EPFNumber : "N/A",
                EmployeeEmail = targetEmployee.Email,
                CurrentBranch = targetEmployee.BranchName,
                CurrentDesignation = !string.IsNullOrWhiteSpace(desigTitle) ? desigTitle : "General Staff",
                // Department routes the request to a Department Head, so it must hold the real
                // department name or nothing at all — a placeholder matches no Department Head
                // and would strand the request at stage 2.
                Department = !string.IsNullOrWhiteSpace(deptTitle) ? deptTitle.Trim() : "",
                RequestedBranch = Input.RequestedBranch,
                Reason = Input.Reason,
                PreferredDate = Input.PreferredDate!.Value,
                YearsOfService = yearsOfService,
                JoinDate = targetEmployee.DateJoined,
                RequestedBy = hrUser.Email ?? hrUser.UserName ?? "HR",
                RequestedByRole = roleName
            };

            await _transferService.CreateTransferRequestAsync(request, documentData, documentFileName, documentContentType);

            TempData["SuccessMessage"] = $"Transfer request for {targetEmployee.FullName} initiated successfully!";
            return RedirectToPage("/Separation/Dashboard", new { ActiveTab = "Transfers" });
        }

        private async Task<ApplicationUser?> GetCurrentUserAsync()
        {
            var username = User.Identity?.Name;
            var user = await _userManager.GetUserAsync(User);
            if (user == null && !string.IsNullOrEmpty(username))
            {
                user = await _userManager.FindByNameAsync(username) ?? await _userManager.FindByEmailAsync(username);
            }
            return user;
        }

        private async Task<HashSet<int>> GetAssignedBranchIdsAsync(ApplicationUser hrUser)
        {
            var assignedIds = new HashSet<int>();
            if (User.IsInRole("HR Manager"))
            {
                return assignedIds; // Empty set means all branches allowed
            }

            if (!string.IsNullOrWhiteSpace(hrUser.ManagedBranches))
            {
                var ids = hrUser.ManagedBranches
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => int.TryParse(s.Trim(), out int id) ? id : 0)
                    .Where(id => id > 0);

                foreach (var id in ids) assignedIds.Add(id);
            }

            if (!string.IsNullOrWhiteSpace(hrUser.Branch) && hrUser.Branch != "Multiple")
            {
                var b = await _context.Branches.FirstOrDefaultAsync(br => br.Name == hrUser.Branch);
                if (b != null) assignedIds.Add(b.Id);
            }

            return assignedIds;
        }

        private async Task<(HashSet<int> DutyEmployeeIds, HashSet<string> DutyIdentifiers)> GetDutyAccountExclusionsAsync()
        {
            var dutyEmployeeIds = new HashSet<int>();
            var dutyIdentifiers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var dutyRoles = new[] { "Admin", "HR Manager", "HR Officer", "Branch Manager", "Area Manager", "Department Head" };
            foreach (var role in dutyRoles)
            {
                var usersInRole = await _userManager.GetUsersInRoleAsync(role);
                foreach (var u in usersInRole)
                {
                    if (u.EmployeeId.HasValue && u.EmployeeId.Value > 0)
                        dutyEmployeeIds.Add(u.EmployeeId.Value);

                    if (!string.IsNullOrWhiteSpace(u.Email))
                        dutyIdentifiers.Add(u.Email.Trim());

                    if (!string.IsNullOrWhiteSpace(u.UserName))
                        dutyIdentifiers.Add(u.UserName.Trim());

                    if (!string.IsNullOrWhiteSpace(u.EpfNumber))
                        dutyIdentifiers.Add(u.EpfNumber.Trim());
                }
            }

            return (dutyEmployeeIds, dutyIdentifiers);
        }

        private async Task LoadAssignedBranchesAndDataAsync(ApplicationUser hrUser, int? selectedBranchId)
        {
            var assignedIds = await GetAssignedBranchIdsAsync(hrUser);
            var allBranchesQuery = _context.Branches.OrderBy(b => b.Name);

            var availableBranches = assignedIds.Any()
                ? await allBranchesQuery.Where(b => assignedIds.Contains(b.Id)).ToListAsync()
                : await allBranchesQuery.ToListAsync();

            AssignedBranchList = availableBranches.Select(b => new SelectListItem
            {
                Value = b.Id.ToString(),
                Text = b.Name,
                Selected = selectedBranchId.HasValue && selectedBranchId.Value == b.Id
            }).ToList();

            AllBranches = await _context.Branches.Select(b => b.Name).OrderBy(b => b).ToListAsync();

            if (selectedBranchId.HasValue)
            {
                var (dutyEmployeeIds, dutyIdentifiers) = await GetDutyAccountExclusionsAsync();
                var rawEmployees = await _context.Employees
                    .Include(e => e.Designation)
                    .Include(e => e.Department)
                    .Include(e => e.Branch)
                    .Where(e => e.BranchId == selectedBranchId.Value 
                             && !e.NIC.StartsWith("DUTY")
                             && e.NIC != "DUTY-ACC" 
                             && e.Status != "Draft" 
                             && e.Status != "Terminated" 
                             && e.Status != "Resigned")
                    .OrderBy(e => e.FullName)
                    .ToListAsync();

                var users = await _context.Users
                    .Where(u => !string.IsNullOrEmpty(u.Designation) || !string.IsNullOrEmpty(u.Department))
                    .Select(u => new { u.EmployeeId, u.Email, u.UserName, u.EpfNumber, u.Designation, u.Department })
                    .ToListAsync();

                var userByEmpId = users.Where(u => u.EmployeeId.HasValue).ToDictionary(u => u.EmployeeId!.Value, u => u);
                var userByEmail = users.Where(u => !string.IsNullOrEmpty(u.Email)).GroupBy(u => u.Email!, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
                var userByEpf = users.Where(u => !string.IsNullOrEmpty(u.EpfNumber)).GroupBy(u => u.EpfNumber!, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

                var filteredEmployees = rawEmployees
                    .Where(e => !dutyEmployeeIds.Contains(e.Id)
                             && (string.IsNullOrEmpty(e.Email) || !dutyIdentifiers.Contains(e.Email.Trim()))
                             && (string.IsNullOrEmpty(e.EPFNumber) || !dutyIdentifiers.Contains(e.EPFNumber.Trim())))
                    .ToList();

                Employees = filteredEmployees.Select(e =>
                {
                    string desig = e.Designation?.Title ?? "";
                    if (string.IsNullOrWhiteSpace(desig))
                    {
                        if (userByEmpId.TryGetValue(e.Id, out var u) && !string.IsNullOrWhiteSpace(u.Designation))
                            desig = u.Designation;
                        else if (!string.IsNullOrEmpty(e.Email) && userByEmail.TryGetValue(e.Email, out u) && !string.IsNullOrWhiteSpace(u.Designation))
                            desig = u.Designation;
                        else if (!string.IsNullOrEmpty(e.EPFNumber) && userByEpf.TryGetValue(e.EPFNumber, out u) && !string.IsNullOrWhiteSpace(u.Designation))
                            desig = u.Designation;
                    }
                    var epf = !string.IsNullOrWhiteSpace(e.EPFNumber) ? e.EPFNumber : "N/A";
                    var desigSuffix = !string.IsNullOrWhiteSpace(desig) ? $" - {desig}" : "";

                    return new SelectListItem
                    {
                        Value = e.Id.ToString(),
                        Text = $"{e.FullName} ({epf}){desigSuffix}",
                        Selected = Input.SelectedEmployeeId.HasValue && Input.SelectedEmployeeId.Value == e.Id
                    };
                }).ToList();
            }
        }
    }
}
