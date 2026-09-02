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
using HRMS.Domain.Entities.Payroll;
using HRMS.Infrastructure.Persistence;
using System.Linq;
using HRMS.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Hosting;

namespace HRMS.UI.Pages.Employees
{
    using Employee = HRMS.Domain.Entities.Core.Employee;

    [Authorize(Roles = "HR Manager,HR Officer,Admin")]
    public class CreateModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IWebHostEnvironment _env;
        private readonly HRMS.Infrastructure.Services.IEmailService _emailService;

        private static readonly string[] AllowedDocumentTypes = ["application/pdf", "image/jpeg", "image/png", "image/jpg"];
        private const long MaxDocumentSizeBytes = 10 * 1024 * 1024;

        public CreateModel(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IWebHostEnvironment env,
            HRMS.Infrastructure.Services.IEmailService emailService)
        {
            _context = context;
            _userManager = userManager;
            _env = env;
            _emailService = emailService;
        }

        [BindProperty]
        public Employee NewEmployee { get; set; } = new Employee { EmployeeType = "Permanent" };

        [BindProperty]
        public int? DraftId { get; set; }

        [BindProperty]
        public int? EmployeeId { get; set; }

        [BindProperty]
        public IFormFile? IdCardScan { get; set; }

        [BindProperty]
        public IFormFile? PoliceClearanceScan { get; set; }

        // Salary Details
        [BindProperty]
        public decimal? BasicSalary { get; set; }

        public SelectList DepartmentList { get; set; } = default!;
        public SelectList DesignationList { get; set; } = default!;
        public SelectList BranchList { get; set; } = default!;
        public SelectList EmployeeTypeList { get; set; } = default!;
        public SelectList SexList { get; set; } = default!;

        [BindProperty]
        public string SelectedRole { get; set; } = "Employee";

        public SelectList RoleList { get; set; } = default!;

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

                    var currentSalary = await _context.PayrollSalaries
                        .Where(s => s.EmployeeId == id.Value)
                        .OrderByDescending(s => s.EffectiveDate)
                        .ThenByDescending(s => s.Id)
                        .FirstOrDefaultAsync();

                    if (currentSalary != null)
                    {
                        BasicSalary = currentSalary.BasicSalary;
                    }

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
                        EmployeeType = string.IsNullOrEmpty(draft.EmployeeType) ? "Permanent" : draft.EmployeeType,
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
                        BankName = draft.BankName ?? "",
                        BankAccountName = draft.BankAccountName ?? "",
                        BankAccountNumber = draft.BankAccountNumber ?? "",
                        DesignationId = draft.DesignationId ?? 0,
                        DateConfirmed = draft.DateConfirmed,
                        ProbationPeriodMonths = draft.ProbationPeriodMonths ?? 0,
                        InternPeriodMonths = draft.InternPeriodMonths ?? 0,
                        PreviousExperienceYears = draft.PreviousExperienceYears ?? 0,
                        DepartmentId = draft.DepartmentId ?? 0,
                        BranchId = draft.BranchId ?? 0,
                    };
                    BasicSalary = draft.BasicSalary;
                }
            }
            else
            {
                if (string.IsNullOrEmpty(NewEmployee.EmployeeType))
                {
                    NewEmployee.EmployeeType = "Permanent";
                }
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
            ModelState.Remove("NewEmployee.BankAccountName");
            ModelState.Remove("NewEmployee.BankAccountNumber");

            if (string.IsNullOrWhiteSpace(NewEmployee.FullName))
            {
                ModelState.AddModelError("NewEmployee.FullName", "Full name is required.");
            }

            if (string.IsNullOrWhiteSpace(NewEmployee.Initials))
            {
                ModelState.AddModelError("NewEmployee.Initials", "Name with initials is required.");
            }

            if (string.IsNullOrWhiteSpace(NewEmployee.Sex))
            {
                ModelState.AddModelError("NewEmployee.Sex", "Please select a gender.");
            }

            if (string.IsNullOrWhiteSpace(NewEmployee.EmployeeType))
            {
                ModelState.AddModelError("NewEmployee.EmployeeType", "Please select an employee type.");
            }

            if (NewEmployee.BranchId <= 0)
            {
                ModelState.AddModelError("NewEmployee.BranchId", "Please select a valid branch.");
            }
            else if (User.IsInRole("HR Officer"))
            {
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser != null && !string.IsNullOrEmpty(currentUser.ManagedBranches))
                {
                    var assignedIds = currentUser.ManagedBranches.Split(',')
                        .Select(s => int.TryParse(s, out var id) ? id : 0)
                        .Where(id => id > 0).ToList();
                    if (!assignedIds.Contains(NewEmployee.BranchId))
                    {
                        ModelState.AddModelError("NewEmployee.BranchId", "You are not authorized to create employees for this branch.");
                    }
                }
                else
                {
                    ModelState.AddModelError("NewEmployee.BranchId", "No branches are currently assigned to your account.");
                }
            }

            if (!NewEmployee.DepartmentId.HasValue || NewEmployee.DepartmentId.Value <= 0)
            {
                ModelState.AddModelError("NewEmployee.DepartmentId", "Please select a valid department.");
            }

            if (!NewEmployee.DesignationId.HasValue || NewEmployee.DesignationId.Value <= 0)
            {
                ModelState.AddModelError("NewEmployee.DesignationId", "Please select a valid designation.");
            }
            else
            {
                var desig = await _context.Designations.FindAsync(NewEmployee.DesignationId.Value);
                if (desig != null)
                {
                    var managerialTitles = new[] { "Branch Manager", "Area Manager", "Department Head" };
                    bool isHrManager = User.IsInRole("HR Manager");

                    if (managerialTitles.Contains(desig.Title) && !isHrManager)
                    {
                        ModelState.AddModelError("NewEmployee.DesignationId", "Only HR Managers are authorized to create or assign managerial employee accounts (Branch Manager, Area Manager, Department Head).");
                    }
                    else if (desig.Title == "Area Manager")
                    {
                        var branch = await _context.Branches.FindAsync(NewEmployee.BranchId);
                        var isHeadOffice = branch != null && (branch.Name == "Head Office" || branch.Name == "Head Office - Colombo" || branch.Name.Contains("Head Office"));

                        var dept = NewEmployee.DepartmentId.HasValue 
                            ? await _context.Departments.FindAsync(NewEmployee.DepartmentId.Value) 
                            : null;
                        var isManagerialDept = dept != null && (dept.Name == "Managerial" || dept.Name == "Management");

                        if (!isHeadOffice || !isManagerialDept)
                        {
                            ModelState.AddModelError("NewEmployee.DesignationId", "Area Manager designation can only be assigned when Branch is 'Head Office' and Department is 'Managerial'.");
                        }
                    }
                    else if (desig.Title == "Branch Manager")
                    {
                        var dept = NewEmployee.DepartmentId.HasValue 
                            ? await _context.Departments.FindAsync(NewEmployee.DepartmentId.Value) 
                            : null;
                        var isManagerialDept = dept != null && (dept.Name == "Managerial" || dept.Name == "Management");
                        if (!isManagerialDept)
                        {
                            ModelState.AddModelError("NewEmployee.DesignationId", "Branch Manager designation must belong to the 'Managerial' department.");
                        }

                        var existingBM = await _context.Employees
                            .Where(e => e.BranchId == NewEmployee.BranchId 
                                        && e.DesignationId == NewEmployee.DesignationId.Value 
                                        && !e.NIC.StartsWith("DUTY")
                                        && e.NIC != "DUTY-ACC" 
                                        && e.Status != "Draft" 
                                        && e.Status != "Terminated" 
                                        && e.Status != "Resigned"
                                        && (!EmployeeId.HasValue || e.Id != EmployeeId.Value))
                            .FirstOrDefaultAsync();

                        if (existingBM != null)
                        {
                            ModelState.AddModelError("NewEmployee.DesignationId", $"A Branch Manager profile ({existingBM.FullName}) already exists for this branch.");
                        }
                    }
                    else if (desig.Title == "Department Head")
                    {
                        var dept = NewEmployee.DepartmentId.HasValue 
                            ? await _context.Departments.FindAsync(NewEmployee.DepartmentId.Value) 
                            : null;
                        var isManagerialDept = dept != null && (dept.Name == "Managerial" || dept.Name == "Management");
                        if (isManagerialDept)
                        {
                            ModelState.AddModelError("NewEmployee.DesignationId", "Department Head designation cannot be assigned to the 'Managerial' department.");
                        }
                        if (NewEmployee.DepartmentId.HasValue && NewEmployee.DepartmentId.Value > 0)
                        {
                            var existingDH = await _context.Employees
                                .Where(e => e.BranchId == NewEmployee.BranchId 
                                            && e.DepartmentId == NewEmployee.DepartmentId.Value
                                            && e.DesignationId == NewEmployee.DesignationId.Value 
                                            && !e.NIC.StartsWith("DUTY")
                                            && e.NIC != "DUTY-ACC" 
                                            && e.Status != "Draft" 
                                            && e.Status != "Terminated" 
                                            && e.Status != "Resigned"
                                            && (!EmployeeId.HasValue || e.Id != EmployeeId.Value))
                                .FirstOrDefaultAsync();

                            if (existingDH != null)
                            {
                                ModelState.AddModelError("NewEmployee.DesignationId", $"A Department Head profile ({existingDH.FullName}) already exists for this department in this branch.");
                            }
                        }
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(NewEmployee.ResidentialAddress))
            {
                ModelState.AddModelError("NewEmployee.ResidentialAddress", "Residential address is required.");
            }

            if (string.IsNullOrWhiteSpace(NewEmployee.EPFNumber))
            {
                ModelState.AddModelError("NewEmployee.EPFNumber", "EPF number is required.");
            }

            if (string.IsNullOrWhiteSpace(NewEmployee.ETFNumber))
            {
                ModelState.AddModelError("NewEmployee.ETFNumber", "ETF number is required.");
            }

            // Server-side Email validation
            var email = NewEmployee.Email?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(email))
            {
                ModelState.AddModelError("NewEmployee.Email", "Email address is required.");
            }
            else if (!Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
            {
                ModelState.AddModelError("NewEmployee.Email", "Please enter a valid email address.");
            }
            else
            {
                var existingUser = await _userManager.FindByEmailAsync(email);
                var existingEmp = await _context.Employees
                    .AnyAsync(e => e.Email == email && !e.NIC.StartsWith("DUTY") && e.NIC != "DUTY-ACC" && (!EmployeeId.HasValue || e.Id != EmployeeId.Value));

                if ((existingUser != null && (!EmployeeId.HasValue || existingUser.EmployeeId != EmployeeId.Value)) || existingEmp)
                {
                    ModelState.AddModelError("NewEmployee.Email", "An employee profile or user account with this email address already exists.");
                }
            }

            // Server-side NIC validation & parsing
            var nic = NewEmployee.NIC?.Trim() ?? "";
            DateTime? nicCalculatedDob = null;
            string? nicCalculatedGender = null;
            if (string.IsNullOrWhiteSpace(nic))
            {
                ModelState.AddModelError("NewEmployee.NIC", "NIC number is required.");
            }
            else
            {
                var (isValidNic, nicError, parsedDob, parsedGender) = ParseSriLankanNic(nic);
                if (!isValidNic)
                {
                    ModelState.AddModelError("NewEmployee.NIC", nicError ?? "Invalid NIC.");
                }
                else
                {
                    nicCalculatedDob = parsedDob;
                    nicCalculatedGender = parsedGender;

                    // If DateOfBirth not set, auto-fill from NIC
                    if (NewEmployee.DateOfBirth == default && parsedDob.HasValue)
                    {
                        NewEmployee.DateOfBirth = parsedDob.Value;
                    }
                    else if (parsedDob.HasValue && NewEmployee.DateOfBirth != default && NewEmployee.DateOfBirth.Date != parsedDob.Value.Date)
                    {
                        ModelState.AddModelError("NewEmployee.NIC",
                            $"NIC indicates Date of Birth {parsedDob.Value:yyyy-MM-dd}, which does not match the entered Date of Birth ({NewEmployee.DateOfBirth:yyyy-MM-dd}).");
                    }

                    // If Sex not set, auto-fill from NIC
                    if (string.IsNullOrWhiteSpace(NewEmployee.Sex) && !string.IsNullOrEmpty(parsedGender))
                    {
                        NewEmployee.Sex = parsedGender;
                    }
                    else if (!string.IsNullOrWhiteSpace(NewEmployee.Sex) && !string.Equals(NewEmployee.Sex, parsedGender, StringComparison.OrdinalIgnoreCase))
                    {
                        ModelState.AddModelError("NewEmployee.NIC",
                            $"NIC indicates gender is {parsedGender}, but '{NewEmployee.Sex}' was selected.");
                    }
                }
            }

            // Server-side Phone validation
            var phone = NewEmployee.PhoneNumber?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(phone))
            {
                ModelState.AddModelError("NewEmployee.PhoneNumber", "Phone number is required.");
            }
            else if (!Regex.IsMatch(phone, @"^0\d{9}$"))
            {
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
            if (dob == default)
            {
                ModelState.AddModelError("NewEmployee.DateOfBirth", "Date of Birth is required.");
            }
            else
            {
                if (dob.Date >= DateTime.Today)
                    ModelState.AddModelError("NewEmployee.DateOfBirth", "Date of Birth must be in the past.");
                else if ((DateTime.Today - dob.Date).TotalDays < 18 * 365.25)
                    ModelState.AddModelError("NewEmployee.DateOfBirth", "Employee must be at least 18 years old.");
            }

            // Server-side Date Joined validation
            if (!NewEmployee.DateJoined.HasValue || NewEmployee.DateJoined.Value == default)
            {
                ModelState.AddModelError("NewEmployee.DateJoined", "Date Joined is required.");
            }
            else
            {
                var dj = NewEmployee.DateJoined.Value.Date;
                if (dj > DateTime.Today)
                    ModelState.AddModelError("NewEmployee.DateJoined", "Date Joined cannot be in the future.");
                else if (dob != default && dj <= dob.Date)
                    ModelState.AddModelError("NewEmployee.DateJoined", "Date Joined must be after Date of Birth.");
            }

            // Server-side Previous Experience & Period validations
            if (NewEmployee.PreviousExperienceYears < 0)
            {
                ModelState.AddModelError("NewEmployee.PreviousExperienceYears", "Previous experience cannot be a negative value.");
            }
            else if (NewEmployee.PreviousExperienceYears > 60)
            {
                ModelState.AddModelError("NewEmployee.PreviousExperienceYears", "Previous experience cannot exceed 60 years.");
            }

            if (NewEmployee.ProbationPeriodMonths < 0)
            {
                ModelState.AddModelError("NewEmployee.ProbationPeriodMonths", "Probation period cannot be a negative value.");
            }

            if (NewEmployee.InternPeriodMonths < 0)
            {
                ModelState.AddModelError("NewEmployee.InternPeriodMonths", "Intern period cannot be a negative value.");
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
            
            NewEmployee.Email = email;
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

            // Resolve display names for the linked login account. ApplicationUser stores these
            // as plain strings, and pages such as Transfer/Apply fall back to them when the
            // Employee navigation properties are not available.
            var designationTitle = NewEmployee.DesignationId.HasValue
                ? await _context.Designations
                    .Where(d => d.Id == NewEmployee.DesignationId.Value)
                    .Select(d => d.Title)
                    .FirstOrDefaultAsync()
                : null;

            var departmentName = NewEmployee.DepartmentId.HasValue
                ? await _context.Departments
                    .Where(d => d.Id == NewEmployee.DepartmentId.Value)
                    .Select(d => d.Name)
                    .FirstOrDefaultAsync()
                : null;

            var branchName = await _context.Branches
                .Where(b => b.Id == NewEmployee.BranchId)
                .Select(b => b.Name)
                .FirstOrDefaultAsync();

            if (EmployeeId.HasValue)
            {
                NewEmployee.Id = EmployeeId.Value;
                _context.Employees.Update(NewEmployee);
                await _context.SaveChangesAsync();
                await SaveEmployeeDocumentsAsync(NewEmployee.Id);

                // Keep the linked login account in step with the employee record
                var userAccount = await _userManager.Users.FirstOrDefaultAsync(u => u.EmployeeId == NewEmployee.Id);
                if (userAccount != null)
                {
                    userAccount.Email = NewEmployee.Email;
                    userAccount.FullName = NewEmployee.FullName;
                    userAccount.EpfNumber = string.IsNullOrWhiteSpace(NewEmployee.EPFNumber)
                        ? "N/A"
                        : NewEmployee.EPFNumber;
                    userAccount.Branch = branchName ?? string.Empty;
                    userAccount.Department = departmentName;
                    userAccount.Designation = designationTitle ?? string.Empty;
                    if (NewEmployee.DateJoined.HasValue)
                        userAccount.DateOfJoining = NewEmployee.DateJoined.Value;

                    await _userManager.UpdateAsync(userAccount);
                }

                // Update or create salary record if specified
                if (BasicSalary.GetValueOrDefault() > 0)
                {
                    var existingSalary = await _context.PayrollSalaries
                        .Where(s => s.EmployeeId == NewEmployee.Id)
                        .OrderByDescending(s => s.EffectiveDate)
                        .ThenByDescending(s => s.Id)
                        .FirstOrDefaultAsync();

                    if (existingSalary == null || existingSalary.BasicSalary != BasicSalary.GetValueOrDefault())
                    {
                        var salary = new PayrollSalary
                        {
                            EmployeeId = NewEmployee.Id,
                            BasicSalary = BasicSalary.GetValueOrDefault(),
                            EffectiveDate = DateTime.Now
                        };
                        _context.PayrollSalaries.Add(salary);
                        await _context.SaveChangesAsync();
                    }
                }
                
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
                    CreatedAt = HRMS.Domain.Common.SriLankaTime.Now
                });
                await _context.SaveChangesAsync();
            }
            else
            {
                _context.Employees.Add(NewEmployee);
                await _context.SaveChangesAsync();
                await SaveEmployeeDocumentsAsync(NewEmployee.Id);

                // Create initial salary record if specified
                if (BasicSalary.GetValueOrDefault() > 0)
                {
                    var newSalary = new PayrollSalary
                    {
                        EmployeeId = NewEmployee.Id,
                        BasicSalary = BasicSalary.GetValueOrDefault(),
                        EffectiveDate = NewEmployee.DateJoined ?? DateTime.Now
                    };
                    _context.PayrollSalaries.Add(newSalary);
                    await _context.SaveChangesAsync();
                }

                // Generate login account with custom username: <surname><initials>.<yy>
                string generatedUsername = GenerateEmployeeUsername(NewEmployee.FullName, NewEmployee.Initials, NewEmployee.DateOfBirth);
                string generatedPassword = $"Kanrich@{new Random().Next(1000, 9999)}";
                var user = new ApplicationUser
                {
                    UserName = generatedUsername,
                    Email = NewEmployee.Email,
                    EmailConfirmed = true,
                    EmployeeId = NewEmployee.Id,
                    FullName = NewEmployee.FullName,
                    EpfNumber = string.IsNullOrWhiteSpace(NewEmployee.EPFNumber)
                        ? "N/A"
                        : NewEmployee.EPFNumber,
                    Branch = branchName ?? string.Empty,
                    Department = departmentName,
                    Designation = designationTitle ?? string.Empty,
                    DateOfJoining = NewEmployee.DateJoined ?? DateTime.Now,
                    MustChangePassword = true
                };

                var result = await _userManager.CreateAsync(user, generatedPassword);
                if (result.Succeeded)
                {
                    await _userManager.AddToRoleAsync(user, "Employee");
                    TempData["SuccessMessage"] = $"Employee {NewEmployee.FullName} created successfully (Username: {generatedUsername}). Login credentials have been emailed to {NewEmployee.Email}.";

                    // Send confidential welcome email to the employee
                    var loginUrl = Url.Page("/Account/Login", pageHandler: null, values: null, protocol: Request.Scheme) 
                                   ?? $"{Request.Scheme}://{Request.Host}/Account/Login";
                    await _emailService.SendWelcomeCredentialsAsync(
                        NewEmployee.Email,
                        NewEmployee.FullName,
                        generatedUsername,
                        generatedPassword,
                        loginUrl);

                    // Add Notification for HR without revealing the plaintext password
                    _context.Notifications.Add(new Notification
                    {
                        UserId = _userManager.GetUserId(User) ?? "",
                        Title = "Employee Account Created",
                        Message = $"Profile for {NewEmployee.FullName} created successfully. Account activation credentials have been securely emailed to {NewEmployee.Email}.",
                        TargetUrl = $"/Employees/Details/{NewEmployee.Id}",
                        IsRead = false,
                        CreatedAt = HRMS.Domain.Common.SriLankaTime.Now
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
                        CreatedAt = HRMS.Domain.Common.SriLankaTime.Now
                    });
                }

                if (DraftId.HasValue)
                {
                    var existingDraft = await _context.DraftEmployees.FindAsync(DraftId.Value);
                    if (existingDraft != null)
                    {
                        _context.DraftEmployees.Remove(existingDraft);
                    }
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
                BasicSalary = BasicSalary,
                BankName = NewEmployee.BankName,
                BankAccountName = NewEmployee.BankAccountName,
                BankAccountNumber = NewEmployee.BankAccountNumber,
                DesignationId = NewEmployee.DesignationId == 0 ? null : NewEmployee.DesignationId,
                DateConfirmed = NewEmployee.DateConfirmed,
                ProbationPeriodMonths = NewEmployee.ProbationPeriodMonths <= 0 ? null : NewEmployee.ProbationPeriodMonths,
                InternPeriodMonths = NewEmployee.InternPeriodMonths <= 0 ? null : NewEmployee.InternPeriodMonths,
                PreviousExperienceYears = NewEmployee.PreviousExperienceYears <= 0 ? null : NewEmployee.PreviousExperienceYears,
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
            var draftName = !string.IsNullOrWhiteSpace(draft.FullName) 
                ? draft.FullName 
                : (!string.IsNullOrWhiteSpace(draft.Initials) ? draft.Initials : "Untitled Draft");
            _context.Notifications.Add(new Notification
            {
                UserId = _userManager.GetUserId(User) ?? "",
                Title = "Draft Saved",
                Message = $"Employee draft for '{draftName}' has been saved.",
                TargetUrl = "/Employees?tab=drafts",
                IsRead = false,
                CreatedAt = HRMS.Domain.Common.SriLankaTime.Now
            });
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Employee saved as draft.";
            return RedirectToPage("./Index", new { tab = "drafts" });
        }

        private async Task LoadDropdownsAsync()
        {
            bool isHrManager = User.IsInRole("HR Manager");
            var managerialTitles = new[] { "Branch Manager", "Area Manager", "Department Head" };

            var managerialDept = await _context.Departments.FirstOrDefaultAsync(d => d.Name == "Managerial" || d.Name == "Management");
            var deps = await _context.Departments.OrderBy(d => d.Name).ToListAsync();

            if (!isHrManager && managerialDept != null)
            {
                deps = deps.Where(d => d.Id != managerialDept.Id).ToList();
            }

            bool isHeadOfficeSelected = false;
            if (NewEmployee.BranchId > 0)
            {
                var br = await _context.Branches.FindAsync(NewEmployee.BranchId);
                if (br != null && (br.Name == "Head Office" || br.Name == "Head Office - Colombo" || br.Name.Contains("Head Office")))
                {
                    isHeadOfficeSelected = true;
                }

                var branchDeptIds = await _context.BranchDepartments
                    .Where(bd => bd.BranchId == NewEmployee.BranchId)
                    .Select(bd => bd.DepartmentId)
                    .ToListAsync();
                if (branchDeptIds.Any())
                {
                    deps = deps.Where(d => branchDeptIds.Contains(d.Id) || (isHrManager && managerialDept != null && d.Id == managerialDept.Id)).ToList();
                }
            }

            bool isManagerialSelected = NewEmployee.DepartmentId.HasValue 
                && managerialDept != null 
                && NewEmployee.DepartmentId.Value == managerialDept.Id;

            List<Designation> combinedDesigs = new();

            if (isManagerialSelected)
            {
                if (isHrManager)
                {
                    var allowedTitles = isHeadOfficeSelected 
                        ? new[] { "Branch Manager", "Area Manager" } 
                        : new[] { "Branch Manager" };

                    combinedDesigs = await _context.Designations
                        .Where(d => allowedTitles.Contains(d.Title))
                        .OrderBy(d => d.Title)
                        .ToListAsync();
                }
            }
            else if (NewEmployee.DepartmentId.HasValue && NewEmployee.DepartmentId.Value > 0)
            {
                List<Designation> deptDesigs = new();
                var deptDesigIds = await _context.DepartmentDesignations
                    .Where(dd => dd.DepartmentId == NewEmployee.DepartmentId.Value)
                    .Select(dd => dd.DesignationId)
                    .ToListAsync();
                if (deptDesigIds.Any())
                {
                    deptDesigs = await _context.Designations
                        .Where(d => deptDesigIds.Contains(d.Id))
                        .ToListAsync();
                }
                else
                {
                    deptDesigs = await _context.Designations.ToListAsync();
                }

                var blockedTitles = new List<string> { "Branch Manager", "Area Manager" };
                if (!isHrManager)
                {
                    blockedTitles.Add("Department Head");
                }

                deptDesigs = deptDesigs.Where(d => !blockedTitles.Contains(d.Title)).ToList();

                if (isHrManager && NewEmployee.DepartmentId.HasValue && NewEmployee.DepartmentId.Value > 0)
                {
                    var deptHeadDesig = await _context.Designations.FirstOrDefaultAsync(d => d.Title == "Department Head");
                    if (deptHeadDesig != null && !deptDesigs.Any(d => d.Id == deptHeadDesig.Id))
                    {
                        deptDesigs.Add(deptHeadDesig);
                    }
                }

                combinedDesigs = deptDesigs
                    .GroupBy(d => d.Id)
                    .Select(g => g.First())
                    .OrderBy(d => d.Title)
                    .ToList();
            }
            else
            {
                // No department selected: keep Designation list empty until department is selected
                combinedDesigs = new List<Designation>();
            }

            var branches = await _context.Branches.OrderBy(b => b.Name).ToListAsync();

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser != null && User.IsInRole("HR Officer") && !string.IsNullOrEmpty(currentUser.ManagedBranches))
            {
                var assignedIds = currentUser.ManagedBranches.Split(',')
                    .Select(s => int.TryParse(s, out var id) ? id : 0)
                    .Where(id => id > 0).ToList();
                if (assignedIds.Any())
                {
                    branches = branches.Where(b => assignedIds.Contains(b.Id)).ToList();
                }
            }

            DepartmentList = new SelectList(deps, "Id", "Name");
            DesignationList = new SelectList(combinedDesigs, "Id", "Title");
            BranchList = new SelectList(branches, "Id", "Name");

            EmployeeTypeList = new SelectList(new[] { "Intern", "Probationary", "Permanent" });
            SexList = new SelectList(new[] { "Male", "Female" });

            SelectedRole = "Employee";
            RoleList = new SelectList(new[] { "Employee" });
        }

        // AJAX endpoint: returns departments assigned to a branch
        public async Task<IActionResult> OnGetDepartmentsByBranchAsync(int branchId)
        {
            if (branchId <= 0) return new JsonResult(new List<object>());

            bool isHrManager = User.IsInRole("HR Manager");

            var branchDeptIds = await _context.BranchDepartments
                .Where(bd => bd.BranchId == branchId)
                .Select(bd => bd.DepartmentId)
                .ToListAsync();

            var managerialDept = await _context.Departments.FirstOrDefaultAsync(d => d.Name == "Managerial" || d.Name == "Management");

            List<Department> departments;
            if (branchDeptIds.Any())
            {
                departments = await _context.Departments
                    .Where(d => branchDeptIds.Contains(d.Id) || (isHrManager && managerialDept != null && d.Id == managerialDept.Id))
                    .OrderBy(d => d.Name)
                    .ToListAsync();
            }
            else
            {
                departments = await _context.Departments
                    .OrderBy(d => d.Name)
                    .ToListAsync();
            }

            if (!isHrManager && managerialDept != null)
            {
                departments = departments.Where(d => d.Id != managerialDept.Id).ToList();
            }

            return new JsonResult(departments.Select(d => new { id = d.Id, name = d.Name }));
        }

        // AJAX endpoint: returns designations assigned to the selected department + universal management designations
        public async Task<IActionResult> OnGetDesignationsByDepartmentAsync(int departmentId, int? branchId)
        {
            bool isHrManager = User.IsInRole("HR Manager");

            bool isHeadOffice = false;
            if (branchId.HasValue && branchId.Value > 0)
            {
                var br = await _context.Branches.FindAsync(branchId.Value);
                if (br != null && (br.Name == "Head Office" || br.Name == "Head Office - Colombo" || br.Name.Contains("Head Office")))
                {
                    isHeadOffice = true;
                }
            }

            var managerialDept = await _context.Departments.FirstOrDefaultAsync(d => d.Name == "Managerial" || d.Name == "Management");
            bool isManagerialDept = departmentId > 0 && managerialDept != null && departmentId == managerialDept.Id;

            // When Department is Managerial:
            if (isManagerialDept)
            {
                if (!isHrManager)
                {
                    return new JsonResult(new List<object>());
                }

                // If Head Office: Branch Manager & Area Manager
                // If Regional Branch: ONLY Branch Manager
                var allowedTitles = isHeadOffice
                    ? new[] { "Branch Manager", "Area Manager" }
                    : new[] { "Branch Manager" };

                var managerialDesigs = await _context.Designations
                    .Where(d => allowedTitles.Contains(d.Title))
                    .OrderBy(d => d.Title)
                    .Select(d => new { id = d.Id, title = d.Title })
                    .ToListAsync();

                return new JsonResult(managerialDesigs);
            }

            // When Department is an Operational Department (IT, Finance, Operations, HR, etc.):
            List<Designation> deptDesigs = new();
            if (departmentId > 0)
            {
                var deptDesigIds = await _context.DepartmentDesignations
                    .Where(dd => dd.DepartmentId == departmentId)
                    .Select(dd => dd.DesignationId)
                    .ToListAsync();

                if (deptDesigIds.Any())
                {
                    deptDesigs = await _context.Designations
                        .Where(d => deptDesigIds.Contains(d.Id))
                        .ToListAsync();
                }
                else
                {
                    deptDesigs = await _context.Designations.ToListAsync();
                }
            }
            else
            {
                deptDesigs = await _context.Designations.ToListAsync();
            }

            // For operational departments:
            // 1. Never include "Branch Manager" or "Area Manager"
            // 2. Include "Department Head" only if HR Manager
            var blockedTitles = new List<string> { "Branch Manager", "Area Manager" };
            if (!isHrManager)
            {
                blockedTitles.Add("Department Head");
            }

            deptDesigs = deptDesigs.Where(d => !blockedTitles.Contains(d.Title)).ToList();

            if (isHrManager && departmentId > 0)
            {
                var deptHeadDesig = await _context.Designations.FirstOrDefaultAsync(d => d.Title == "Department Head");
                if (deptHeadDesig != null && !deptDesigs.Any(d => d.Id == deptHeadDesig.Id))
                {
                    deptDesigs.Add(deptHeadDesig);
                }
            }

            var result = deptDesigs
                .GroupBy(d => d.Id)
                .Select(g => g.First())
                .OrderBy(d => d.Title)
                .Select(d => new { id = d.Id, title = d.Title })
                .ToList();

            return new JsonResult(result);
        }

        // AJAX endpoint: checks if a Branch Manager or Department Head already exists
        public async Task<IActionResult> OnGetCheckDesignationAvailabilityAsync(int branchId, int? departmentId, int designationId, int? employeeId)
        {
            if (branchId <= 0 || designationId <= 0)
                return new JsonResult(new { isAvailable = true });

            var desig = await _context.Designations.FindAsync(designationId);
            if (desig == null)
                return new JsonResult(new { isAvailable = true });

            if (desig.Title == "Branch Manager")
            {
                var existing = await _context.Employees
                    .Where(e => e.BranchId == branchId 
                                && e.DesignationId == designationId 
                                && !e.NIC.StartsWith("DUTY")
                                && e.NIC != "DUTY-ACC" 
                                && e.Status != "Draft" 
                                && e.Status != "Terminated" 
                                && e.Status != "Resigned"
                                && (!employeeId.HasValue || e.Id != employeeId.Value))
                    .Select(e => new { e.Id, e.FullName })
                    .FirstOrDefaultAsync();

                if (existing != null)
                {
                    return new JsonResult(new { isAvailable = false, message = $"A Branch Manager profile ({existing.FullName}) already exists for this branch." });
                }
            }
            else if (desig.Title == "Department Head" && departmentId.HasValue && departmentId.Value > 0)
            {
                var existing = await _context.Employees
                    .Where(e => e.BranchId == branchId 
                                && e.DepartmentId == departmentId.Value
                                && e.DesignationId == designationId 
                                && !e.NIC.StartsWith("DUTY")
                                && e.NIC != "DUTY-ACC" 
                                && e.Status != "Draft" 
                                && e.Status != "Terminated" 
                                && e.Status != "Resigned"
                                && (!employeeId.HasValue || e.Id != employeeId.Value))
                    .Select(e => new { e.Id, e.FullName })
                    .FirstOrDefaultAsync();

                if (existing != null)
                {
                    return new JsonResult(new { isAvailable = false, message = $"A Department Head profile ({existing.FullName}) already exists for this department in this branch." });
                }
            }

            return new JsonResult(new { isAvailable = true });
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
            if (currentUser == null) return 0;
            
            Domain.Entities.Core.Employee? hrEmp = null;
            if (currentUser.EmployeeId.HasValue)
            {
                hrEmp = await _context.Employees.FindAsync(currentUser.EmployeeId.Value);
            }
            else
            {
                hrEmp = await _context.Employees.FirstOrDefaultAsync(e => e.Email == currentUser.Email);
            }

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

        public static string GenerateEmployeeUsername(string fullName, string initials, DateTime dob)
        {
            var yy = (dob != default ? dob.Year % 100 : DateTime.Now.Year % 100).ToString("D2");
            
            fullName = fullName?.Trim() ?? string.Empty;
            initials = initials?.Trim() ?? string.Empty;

            string surname = "";
            string initialsPart = "";

            // Case 1: Check if initials contains dotted initials with surname e.g. "H.D.R.N. Senarath"
            if (!string.IsNullOrWhiteSpace(initials))
            {
                var initTokens = initials.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (initTokens.Length > 1 && !initTokens.Last().Contains('.'))
                {
                    surname = initTokens.Last();
                    var initOnly = string.Join("", initTokens.Take(initTokens.Length - 1));
                    initialsPart = Regex.Replace(initOnly, @"[^a-zA-Z]", "");
                }
                else if (initTokens.All(t => t.Contains('.') || t.Length == 1))
                {
                    initialsPart = Regex.Replace(initials, @"[^a-zA-Z]", "");
                }
            }

            // Case 2: Extract surname from FullName if not yet resolved
            if (string.IsNullOrWhiteSpace(surname) && !string.IsNullOrWhiteSpace(fullName))
            {
                var nameTokens = fullName.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                surname = nameTokens.Last();

                if (string.IsNullOrWhiteSpace(initialsPart))
                {
                    var prior = nameTokens.Take(nameTokens.Length - 1);
                    var initLetters = string.Concat(prior.Select(t => t.Contains('.') ? Regex.Replace(t, @"[^a-zA-Z]", "") : (t.Length > 0 ? t.Substring(0, 1) : "")));
                    initialsPart = initLetters;
                }
            }

            var cleanSurname = Regex.Replace(surname, @"[^a-zA-Z0-9]", "").ToLowerInvariant();
            var cleanInitials = Regex.Replace(initialsPart, @"[^a-zA-Z0-9]", "").ToLowerInvariant();

            if (string.IsNullOrEmpty(cleanSurname))
                cleanSurname = "user";

            return $"{cleanSurname}{cleanInitials}.{yy}";
        }

        public static (bool IsValid, string? Error, DateTime? Dob, string? Gender) ParseSriLankanNic(string nic)
        {
            if (string.IsNullOrWhiteSpace(nic))
                return (false, "NIC number is required.", null, null);

            nic = nic.Trim();
            int birthYear;
            int dayOfYear;

            if (Regex.IsMatch(nic, @"^(\d{2})(\d{3})(\d{4})[VvXx]$"))
            {
                birthYear = 1900 + int.Parse(nic.Substring(0, 2));
                dayOfYear = int.Parse(nic.Substring(2, 3));
            }
            else if (Regex.IsMatch(nic, @"^(\d{4})(\d{3})(\d{5})$"))
            {
                birthYear = int.Parse(nic.Substring(0, 4));
                dayOfYear = int.Parse(nic.Substring(4, 3));
            }
            else
            {
                return (false, "Invalid NIC format. Use 9 digits with V/X (e.g. 901234567V) or 12 digits (e.g. 200301500132).", null, null);
            }

            string gender = "Male";
            int days = dayOfYear;
            if (dayOfYear > 500)
            {
                gender = "Female";
                days = dayOfYear - 500;
            }

            if (days < 1 || days > 366)
            {
                return (false, $"Invalid NIC day digits '{dayOfYear:D3}'. Days must be 001-366 (Male) or 501-866 (Female).", null, null);
            }

            // Month days in standard SL NIC mapping (Feb treated as 29 days)
            int[] monthDays = { 31, 29, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31 };
            int month = 1;
            int day = days;
            for (int i = 0; i < monthDays.Length; i++)
            {
                if (day <= monthDays[i])
                {
                    month = i + 1;
                    break;
                }
                day -= monthDays[i];
            }

            // Handle non-leap year Feb 29 adjustment
            if (month == 2 && day == 29 && !DateTime.IsLeapYear(birthYear))
            {
                day = 28;
            }

            try
            {
                var dob = new DateTime(birthYear, month, day);
                return (true, null, dob, gender);
            }
            catch
            {
                return (false, "Unable to compute valid Date of Birth from NIC.", null, null);
            }
        }
    }
}




