using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using HRMS.Domain.Entities.Core;
using HRMS.Infrastructure.Persistence;
using System.Linq;
using HRMS.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Hosting;

namespace HRMS.UI.Pages.Employees
{
    [Authorize(Roles = "HR Manager")]
    public class CreateModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IWebHostEnvironment _env;

        private static readonly string[] AllowedDocumentTypes = ["application/pdf", "image/jpeg", "image/png", "image/jpg"];
        private const long MaxDocumentSizeBytes = 10 * 1024 * 1024;

        public CreateModel(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IWebHostEnvironment env)
        {
            _context = context;
            _userManager = userManager;
            _env = env;
        }

        [BindProperty]
        public Employee NewEmployee { get; set; } = new Employee();

        [BindProperty]
        public int? DraftId { get; set; }

        [BindProperty]
        public int? EmployeeId { get; set; }

        [BindProperty]
        public IFormFile? IdCardScan { get; set; }

        [BindProperty]
        public IFormFile? PoliceClearanceScan { get; set; }

        public SelectList DepartmentList { get; set; } = default!;
        public SelectList DesignationList { get; set; } = default!;
        public SelectList BranchList { get; set; } = default!;
        public SelectList EmployeeTypeList { get; set; } = default!;
        public SelectList SexList { get; set; } = default!;

        [BindProperty]
        public string SelectedRole { get; set; } = "Employee";

        public SelectList RoleList { get; set; } = default!;

        // HR Manager's assigned branch (used to lock branch selection)
        public int HrManagerBranchId { get; set; }
        public string HrManagerBranchName { get; set; } = string.Empty;

        public async Task<IActionResult> OnGetAsync(int? id, int? draftId)
        {
            await LoadDropdownsAsync();
            
            if (id.HasValue)
            {
                EmployeeId = id.Value;
                var emp = await _context.Employees.FindAsync(id.Value);
                if (emp != null)
                {
                    NewEmployee = emp;
                    return Page();
                }
            }
            else if (draftId.HasValue)
            {
                var draft = await _context.DraftEmployees.FindAsync(draftId.Value);
                if (draft != null)
                {
                    DraftId = draft.Id;
                    NewEmployee = new Employee
                    {
                        FullName = draft.FullName ?? "",
                        Initials = draft.Initials ?? "",
                        Sex = draft.Sex ?? "",
                        EmployeeType = draft.EmployeeType ?? "",
                        NIC = draft.NIC ?? "",
                        DateOfBirth = draft.DateOfBirth ?? default,
                        DateJoined = draft.DateJoined,
                        Email = draft.Email ?? "",
                        PhoneNumber = draft.PhoneNumber ?? "",
                        ResidentialAddress = draft.ResidentialAddress ?? "",
                        SpouseName = draft.SpouseName,
                        SpouseContactNo = draft.SpouseContactNo,
                        EPFNumber = draft.EPFNumber ?? "",
                        ETFNumber = draft.ETFNumber ?? "",
                        BankAccountName = draft.BankAccountName ?? "",
                        BankAccountNumber = draft.BankAccountNumber ?? "",
                        DesignationId = draft.DesignationId ?? 0,
                        DateConfirmed = draft.DateConfirmed,
                        ProbationPeriodMonths = draft.ProbationPeriodMonths ?? 0,
                        InternPeriodMonths = draft.InternPeriodMonths ?? 0,
                        PreviousExperienceYears = draft.PreviousExperienceYears ?? 0,
                        DepartmentId = draft.DepartmentId ?? 0,
                    };
                }
            }

            // Always lock branch to the HR Manager's own branch
            if (User.IsInRole("HR Manager"))
            {
                HrManagerBranchId = await GetHrManagerBranchIdAsync();
                NewEmployee.BranchId = HrManagerBranchId;
                HrManagerBranchName = (await _context.Branches.FindAsync(HrManagerBranchId))?.Name ?? string.Empty;
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            ModelState.Remove("NewEmployee.Department");
            ModelState.Remove("NewEmployee.Designation");
            ModelState.Remove("NewEmployee.Branch");
            ModelState.Remove("NewEmployee.ReportingOfficer");
            ModelState.Remove("NewEmployee.Status");
            ModelState.Remove("NewEmployee.DateJoined");
            ModelState.Remove("NewEmployee.Email");
            ModelState.Remove("NewEmployee.BankAccountName");
            ModelState.Remove("NewEmployee.BankAccountNumber");

            // Force HR Manager's branch — cannot be overridden via form input
            if (User.IsInRole("HR Manager"))
            {
                NewEmployee.BranchId = await GetHrManagerBranchIdAsync();
                ModelState.Remove("NewEmployee.BranchId");
            }

            // Server-side NIC validation
            var nic = NewEmployee.NIC?.Trim() ?? "";
            if (!string.IsNullOrEmpty(nic))
            {
                bool validNic = Regex.IsMatch(nic, @"^\d{9}[VvXx]$") || Regex.IsMatch(nic, @"^\d{12}$");
                if (!validNic)
                    ModelState.AddModelError("NewEmployee.NIC", "Invalid NIC. Use 9 digits + V/X or 12 digits.");
            }

            // Server-side Phone validation
            var phone = NewEmployee.PhoneNumber?.Trim() ?? "";
            if (!string.IsNullOrEmpty(phone))
            {
                if (!Regex.IsMatch(phone, @"^0\d{9}$"))
                    ModelState.AddModelError("NewEmployee.PhoneNumber", "Phone must start with 0 and have exactly 10 digits.");
            }

            // Server-side Spouse Phone validation
            var spousePhone = NewEmployee.SpouseContactNo?.Trim() ?? "";
            if (!string.IsNullOrEmpty(spousePhone))
            {
                if (!Regex.IsMatch(spousePhone, @"^0\d{9}$"))
                    ModelState.AddModelError("NewEmployee.SpouseContactNo", "Spouse Phone must start with 0 and have exactly 10 digits if provided.");
            }

            // Server-side Date of Birth validation
            var dob = NewEmployee.DateOfBirth;
            if (dob != default)
            {
                if (dob.Date >= DateTime.Today)
                    ModelState.AddModelError("NewEmployee.DateOfBirth", "Date of Birth must be in the past.");
                else if ((DateTime.Today - dob.Date).TotalDays < 18 * 365.25)
                    ModelState.AddModelError("NewEmployee.DateOfBirth", "Employee must be at least 18 years old.");

                // NIC birth year cross-check (only when NIC format is valid)
                if (!string.IsNullOrEmpty(nic))
                {
                    bool isOldNic = Regex.IsMatch(nic, @"^\d{9}[VvXx]$");
                    bool isNewNic = Regex.IsMatch(nic, @"^\d{12}$");
                    if (isOldNic || isNewNic)
                    {
                        int nicYear = isOldNic
                            ? 1900 + int.Parse(nic.Substring(0, 2))
                            : int.Parse(nic.Substring(0, 4));
                        if (dob.Year != nicYear)
                            ModelState.AddModelError("NewEmployee.NIC",
                                $"Birth year in NIC ({nicYear}) does not match Date of Birth year ({dob.Year}).");
                    }
                }
            }

            // Server-side Date Joined validation (only when a value is provided)
            if (NewEmployee.DateJoined.HasValue)
            {
                var dj = NewEmployee.DateJoined.Value.Date;
                if (dj > DateTime.Today)
                    ModelState.AddModelError("NewEmployee.DateJoined", "Date Joined cannot be in the future.");
                else if (dob != default && dj <= dob.Date)
                    ModelState.AddModelError("NewEmployee.DateJoined", "Date Joined must be after Date of Birth.");
            }

            if (!ModelState.IsValid)
            {
                await LoadDropdownsAsync();
                return Page();
            }

            ValidateDocumentFile(IdCardScan, "IdCardScan", "ID card scan");
            ValidateDocumentFile(PoliceClearanceScan, "PoliceClearanceScan", "Police clearance document");

            if (!ModelState.IsValid)
            {
                await LoadDropdownsAsync();
                return Page();
            }


            if (NewEmployee.DateJoined == default)
            {
                 NewEmployee.DateJoined = DateTime.Now;
            }
            if (string.IsNullOrEmpty(NewEmployee.Email) || NewEmployee.Email == "placeholder@example.com")
            {
                NewEmployee.Email = GenerateEmployeeEmail(NewEmployee.Initials, NewEmployee.DateOfBirth);
            }
            if (string.IsNullOrEmpty(NewEmployee.BankAccountName)) NewEmployee.BankAccountName = "-";
            if (string.IsNullOrEmpty(NewEmployee.BankAccountNumber)) NewEmployee.BankAccountNumber = "-";

            // If JS failed to auto-fill, fallback logic:
            if (string.IsNullOrWhiteSpace(NewEmployee.Initials))
            {
                NewEmployee.Initials = GenerateNameWithInitials(NewEmployee.FullName);
            }

            NewEmployee.Status = "Active";

            if (DraftId.HasValue)
            {
                var existingDraft = await _context.DraftEmployees.FindAsync(DraftId.Value);
                if (existingDraft != null)
                {
                    _context.DraftEmployees.Remove(existingDraft);
                }
            }

            if (EmployeeId.HasValue)
            {
                NewEmployee.Id = EmployeeId.Value;
                _context.Employees.Update(NewEmployee);
                await _context.SaveChangesAsync();
                await SaveEmployeeDocumentsAsync(NewEmployee.Id);
                
                var successMsg = $"Employee {NewEmployee.FullName} updated successfully.";
                TempData["SuccessMessage"] = successMsg;

                // Add Notification
                _context.Notifications.Add(new Notification
                {
                    UserId = _userManager.GetUserId(User) ?? "",
                    Title = "Employee Updated",
                    Message = successMsg,
                    TargetUrl = $"/Employees/Details/{NewEmployee.Id}",
                    IsRead = false,
                    CreatedAt = DateTime.Now
                });
                await _context.SaveChangesAsync();
            }
            else
            {
                _context.Employees.Add(NewEmployee);
                await _context.SaveChangesAsync();
                await SaveEmployeeDocumentsAsync(NewEmployee.Id);

                // Generate login account without trailing punctuation
                string generatedPassword = $"Kanrich@{new Random().Next(1000, 9999)}";
                var user = new ApplicationUser
                {
                    UserName = NewEmployee.Email,
                    Email = NewEmployee.Email,
                    EmailConfirmed = true,
                    EmployeeId = NewEmployee.Id
                };

                var result = await _userManager.CreateAsync(user, generatedPassword);
                if (result.Succeeded)
                {
                    var roleToAssign = string.IsNullOrEmpty(SelectedRole) ? "Employee" : SelectedRole;
                    await _userManager.AddToRoleAsync(user, roleToAssign);
                    TempData["SuccessMessage"] = $"Employee {NewEmployee.FullName} created successfully with role {roleToAssign}.";

                    // Add Notification with credentials
                    _context.Notifications.Add(new Notification
                    {
                        UserId = _userManager.GetUserId(User) ?? "",
                        Title = "Employee Account Created",
                        Message = $"Profile for {NewEmployee.FullName} created successfully. \nEmail: {NewEmployee.Email} \nPassword: {generatedPassword}",
                        TargetUrl = $"/Employees/Details/{NewEmployee.Id}",
                        IsRead = false,
                        CreatedAt = DateTime.Now
                    });
                }
                else
                {
                    string errors = string.Join(", ", result.Errors.Select(e => e.Description));
                    TempData["SuccessMessage"] = $"Employee {NewEmployee.FullName} created, but account generation failed.";
                    
                    _context.Notifications.Add(new Notification
                    {
                        UserId = _userManager.GetUserId(User) ?? "",
                        Title = "Account Generation Failed",
                        Message = $"Employee {NewEmployee.FullName} was created, but identity account failed: {errors}",
                        TargetUrl = $"/Employees/Details/{NewEmployee.Id}",
                        IsRead = false,
                        CreatedAt = DateTime.Now
                    });
                }
                await _context.SaveChangesAsync();
            }

            return RedirectToPage("./Index");
        }

        public async Task<IActionResult> OnPostDraftAsync()
        {
            // Bypass all validation for drafts
            ModelState.Clear();

            // Force HR Manager's branch before mapping
            if (User.IsInRole("HR Manager"))
                NewEmployee.BranchId = await GetHrManagerBranchIdAsync();

            // Map data to the nullable DraftEmployee entity
            var draft = new DraftEmployee
            {
                FullName = NewEmployee.FullName,
                Initials = string.IsNullOrWhiteSpace(NewEmployee.Initials) 
                           ? GenerateNameWithInitials(NewEmployee.FullName) 
                           : NewEmployee.Initials,
                Sex = NewEmployee.Sex,
                EmployeeType = NewEmployee.EmployeeType,
                NIC = NewEmployee.NIC,
                DateOfBirth = NewEmployee.DateOfBirth == default ? null : NewEmployee.DateOfBirth,
                DateJoined = NewEmployee.DateJoined == default ? null : NewEmployee.DateJoined,
                Email = NewEmployee.Email,
                PhoneNumber = NewEmployee.PhoneNumber,
                ResidentialAddress = NewEmployee.ResidentialAddress,
                SpouseName = NewEmployee.SpouseName,
                SpouseContactNo = NewEmployee.SpouseContactNo,
                EPFNumber = NewEmployee.EPFNumber,
                ETFNumber = NewEmployee.ETFNumber,
                BankAccountName = NewEmployee.BankAccountName,
                BankAccountNumber = NewEmployee.BankAccountNumber,
                DesignationId = NewEmployee.DesignationId == 0 ? null : NewEmployee.DesignationId,
                DateConfirmed = NewEmployee.DateConfirmed,
                ProbationPeriodMonths = NewEmployee.ProbationPeriodMonths == 0 ? null : NewEmployee.ProbationPeriodMonths,
                InternPeriodMonths = NewEmployee.InternPeriodMonths == 0 ? null : NewEmployee.InternPeriodMonths,
                PreviousExperienceYears = NewEmployee.PreviousExperienceYears == 0 ? null : NewEmployee.PreviousExperienceYears,
                Status = "Draft",
                LastUpdated = DateTime.Now,
                DepartmentId = NewEmployee.DepartmentId == 0 ? null : NewEmployee.DepartmentId,
                BranchId = NewEmployee.BranchId == 0 ? null : NewEmployee.BranchId
            };

            if (DraftId.HasValue)
            {
                var existingDraft = await _context.DraftEmployees.FindAsync(DraftId.Value);
                if (existingDraft != null)
                {
                    _context.DraftEmployees.Remove(existingDraft);
                }
            }

            _context.DraftEmployees.Add(draft);
            await _context.SaveChangesAsync();

            // Add Notification
            _context.Notifications.Add(new Notification
            {
                UserId = _userManager.GetUserId(User) ?? "",
                Title = "Draft Saved",
                Message = $"Employee draft for '{NewEmployee.FullName}' has been saved.",
                TargetUrl = "/Employees?tab=drafts",
                IsRead = false,
                CreatedAt = DateTime.Now
            });
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Employee saved as draft.";
            return RedirectToPage("./Index", new { tab = "drafts" });
        }

        private async Task LoadDropdownsAsync()
        {
            var deps = await _context.Departments.ToListAsync();
            var desigs = await _context.Designations.ToListAsync();
            var branches = await _context.Branches.ToListAsync();
            DepartmentList = new SelectList(deps, "Id", "Name");
            DesignationList = new SelectList(desigs, "Id", "Title");
            BranchList = new SelectList(branches, "Id", "Name");

            EmployeeTypeList = new SelectList(new[] { "Intern", "Probationary", "Permanent" });
            SexList = new SelectList(new[] { "Male", "Female" });

            List<string> roles;
            if (User.IsInRole("Admin"))
            {
                // Admins create duty accounts via Settings → Duty Accounts, not here.
                // Keep only Employee for normal onboarding; Admin for completeness.
                roles = new List<string> { "HR Manager", "Area Manager", "Branch Manager", "Admin" };
                if (string.IsNullOrEmpty(SelectedRole)) SelectedRole = "HR Manager";
            }
            else
            {
                // HR Managers (and all other roles) can only create normal Employee accounts.
                roles = new List<string> { "Employee" };
                SelectedRole = "Employee";
            }
            RoleList = new SelectList(roles);
        }

        // AJAX endpoint: returns departments assigned to a branch
        public async Task<IActionResult> OnGetDepartmentsByBranchAsync(int branchId)
        {
            var departments = await _context.BranchDepartments
                .Where(bd => bd.BranchId == branchId)
                .OrderBy(bd => bd.Department.Name)
                .Select(bd => new { id = bd.Department.Id, name = bd.Department.Name })
                .ToListAsync();

            return new JsonResult(departments);
        }

        // AJAX endpoint: returns designations assigned to a department
        public async Task<IActionResult> OnGetDesignationsByDepartmentAsync(int departmentId)
        {
            var designations = await _context.DepartmentDesignations
                .Where(dd => dd.DepartmentId == departmentId)
                .Select(dd => new { id = dd.Designation.Id, title = dd.Designation.Title })
                .ToListAsync();

            return new JsonResult(designations);
        }

        private string GenerateNameWithInitials(string fullName)
        {
            if (string.IsNullOrWhiteSpace(fullName)) return string.Empty;
            var parts = fullName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 1) return parts[0];
            
            var initials = string.Join(" ", parts.Take(parts.Length - 1).Select(p => p[0] + "."));
            return $"{initials} {parts.Last()}";
        }

        private string GenerateEmployeeEmail(string nameWithInitials, DateTime dob)
        {
            if (string.IsNullOrWhiteSpace(nameWithInitials)) return $"emp{DateTime.Now.Ticks}@kanrich.lk";

            var parts = nameWithInitials.Split(new[] { ' ', '.' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return $"emp{DateTime.Now.Ticks}@kanrich.lk";
            
            string lastName;
            string initials = "";
            
            if (parts.Length == 1)
            {
                lastName = parts[0].ToLowerInvariant();
            }
            else
            {
                lastName = parts.Last().ToLowerInvariant();
                initials = string.Join("", parts.Take(parts.Length - 1)).ToLowerInvariant();
            }
            
            var year = dob.ToString("yy");
            return $"{lastName}{initials}.{year}@kanrich.lk";
        }

        private void ValidateDocumentFile(IFormFile? file, string key, string label)
        {
            if (file == null || file.Length == 0)
            {
                return;
            }

            if (file.Length > MaxDocumentSizeBytes)
            {
                ModelState.AddModelError(key, $"{label} must not exceed 10 MB.");
            }

            if (!AllowedDocumentTypes.Contains(file.ContentType))
            {
                ModelState.AddModelError(key, $"{label} must be a PDF, JPG, or PNG file.");
            }
        }

        private async Task<int> GetHrManagerBranchIdAsync()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser?.EmployeeId == null) return 0;
            var hrEmp = await _context.Employees.FindAsync(currentUser.EmployeeId.Value);
            return hrEmp?.BranchId ?? 0;
        }

        private async Task SaveEmployeeDocumentsAsync(int employeeId)
        {
            var filesToSave = new List<(IFormFile? File, string DocumentType)>
            {
                (IdCardScan, "Scanned ID Card"),
                (PoliceClearanceScan, "Police Clearance Document")
            };

            var uploadsDir = Path.Combine(_env.WebRootPath, "uploads", "documents");
            Directory.CreateDirectory(uploadsDir);

            foreach (var (file, documentType) in filesToSave)
            {
                if (file == null || file.Length == 0)
                {
                    continue;
                }

                var ext = Path.GetExtension(file.FileName);
                var storedName = $"{Guid.NewGuid()}{ext}";
                var filePath = Path.Combine(uploadsDir, storedName);

                using var stream = new FileStream(filePath, FileMode.Create);
                await file.CopyToAsync(stream);

                _context.EmployeeDocuments.Add(new EmployeeDocument
                {
                    EmployeeId = employeeId,
                    DocumentType = documentType,
                    FileName = file.FileName,
                    StoredFileName = storedName,
                    ContentType = file.ContentType,
                    UploadedAt = DateTime.Now,
                    Status = "Pending"
                });
            }

            var currentUserId = _userManager.GetUserId(User);
            var hasNewDocuments = false;

            foreach (var entry in _context.ChangeTracker.Entries<EmployeeDocument>()
                .Where(e => e.State == EntityState.Added && e.Entity.EmployeeId == employeeId))
            {
                entry.Entity.Status = "Approved";
                entry.Entity.ReviewedAt = DateTime.Now;
                entry.Entity.ReviewedByUserId = currentUserId;
                entry.Entity.ReviewerNotes = "Verified and uploaded by HR during profile creation.";
                hasNewDocuments = true;
            }

            if (hasNewDocuments)
            {
                await _context.SaveChangesAsync();
            }
        }
    }
}




