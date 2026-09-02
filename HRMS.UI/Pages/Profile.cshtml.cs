using HRMS.Domain.Entities.Core;
using HRMS.Domain.Entities.Payroll;
using HRMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using HRMS.Infrastructure.Identity;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace HRMS.UI.Pages
{
    [Authorize]

    public class ProfileModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly UserManager<ApplicationUser> _userManager;

        public ProfileModel(ApplicationDbContext context, IWebHostEnvironment env, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _env = env;
            _userManager = userManager;
        }

        public HRMS.Domain.Entities.Core.Employee? Employee { get; set; }
        public List<EmployeeDocument> Documents { get; set; } = new();
        public PayrollSalary? CurrentSalary { get; set; }
        public List<PayrollSalary> SalaryHistory { get; set; } = new();

        /// <summary>
        /// Designation resolved from the employee record, falling back to the linked login
        /// account when the employee has no designation set. Empty when neither source has one.
        /// </summary>
        public string DesignationTitle { get; set; } = string.Empty;

        [BindProperty]
        public string DocumentType { get; set; } = string.Empty;

        [BindProperty]
        public IFormFile? UploadedFile { get; set; }

        public string? UploadError { get; set; }
        public string? UploadSuccess { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            await LoadAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostUploadAvatarAsync(IFormFile? avatarFile)
        {
            await LoadAsync();

            if (Employee == null)
            {
                TempData["AvatarError"] = "Employee record not found for your account.";
                return RedirectToPage();
            }

            if (avatarFile == null || avatarFile.Length == 0)
            {
                TempData["AvatarError"] = "Please select an image file to upload.";
                return RedirectToPage();
            }

            if (avatarFile.Length > 5 * 1024 * 1024)
            {
                TempData["AvatarError"] = "Image size must not exceed 5 MB.";
                return RedirectToPage();
            }

            var allowedExts = new[] { ".jpg", ".jpeg", ".png", ".webp" };
            var ext = Path.GetExtension(avatarFile.FileName).ToLowerInvariant();
            if (!allowedExts.Contains(ext))
            {
                TempData["AvatarError"] = "Only JPG, PNG, or WEBP images are allowed.";
                return RedirectToPage();
            }

            try
            {
                var avatarsDir = Path.Combine(_env.WebRootPath, "uploads", "avatars");
                if (!Directory.Exists(avatarsDir))
                {
                    Directory.CreateDirectory(avatarsDir);
                }

                var filePath = Path.Combine(avatarsDir, $"emp_{Employee.Id}.jpg");
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await avatarFile.CopyToAsync(stream);
                }

                TempData["AvatarSuccess"] = "Profile picture updated successfully!";
            }
            catch (Exception ex)
            {
                TempData["AvatarError"] = "Failed to save profile picture: " + ex.Message;
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostUploadAsync()
        {
            await LoadAsync();

            if (Employee == null)
            {
                TempData["UploadError"] = "Employee record not found for your account.";
                return RedirectToPage();
            }

            // Validate document type
            if (string.IsNullOrWhiteSpace(DocumentType))
            {
                TempData["UploadError"] = "Please select a document type.";
                return RedirectToPage();
            }

            // Validate file
            if (UploadedFile == null || UploadedFile.Length == 0)
            {
                TempData["UploadError"] = "Please select a file to upload.";
                return RedirectToPage();
            }

            // Max 10 MB
            if (UploadedFile.Length > 10 * 1024 * 1024)
            {
                TempData["UploadError"] = "File size must not exceed 10 MB.";
                return RedirectToPage();
            }

            // Allowed types
            var allowedTypes = new[] { "application/pdf", "image/jpeg", "image/png", "image/jpg" };
            if (!allowedTypes.Contains(UploadedFile.ContentType))
            {
                TempData["UploadError"] = "Only PDF, JPG, and PNG files are allowed.";
                return RedirectToPage();
            }

            // Save file
            var uploadsDir = Path.Combine(_env.WebRootPath, "uploads", "documents");
            Directory.CreateDirectory(uploadsDir);

            var ext = Path.GetExtension(UploadedFile.FileName);
            var storedName = $"{Guid.NewGuid()}{ext}";
            var filePath = Path.Combine(uploadsDir, storedName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await UploadedFile.CopyToAsync(stream);
            }

            // Save record
            var doc = new EmployeeDocument
            {
                EmployeeId   = Employee.Id,
                DocumentType = DocumentType,
                FileName     = UploadedFile.FileName,
                StoredFileName = storedName,
                ContentType  = UploadedFile.ContentType,
                UploadedAt   = DateTime.Now,
                Status       = "Pending"
            };
            _context.EmployeeDocuments.Add(doc);

            // Fetch HR Officers / Admin users to notify (HR Manager is explicitly excluded)
            var hrOfficers = await _userManager.GetUsersInRoleAsync("HR Officer");
            var empBranchId = Employee.BranchId;
            var targetOfficerIds = new List<string>();

            foreach (var officer in hrOfficers)
            {
                if (string.IsNullOrWhiteSpace(officer.ManagedBranches))
                {
                    targetOfficerIds.Add(officer.Id);
                }
                else
                {
                    var bIds = officer.ManagedBranches.Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(s => int.TryParse(s.Trim(), out var b) ? b : 0)
                        .Where(b => b > 0).ToList();
                    if (bIds.Contains(empBranchId))
                    {
                        targetOfficerIds.Add(officer.Id);
                    }
                }
            }

            // Fallback: If no officer is specifically mapped to this branch, notify all HR Officers
            if (!targetOfficerIds.Any() && hrOfficers.Any())
            {
                targetOfficerIds.AddRange(hrOfficers.Select(o => o.Id));
            }

            var adminUsers = await _userManager.GetUsersInRoleAsync("Admin");
            var notifyUsers = targetOfficerIds.Concat(adminUsers.Select(u => u.Id)).Distinct();

            foreach (var recipientId in notifyUsers)
            {
                _context.Notifications.Add(new Notification
                {
                    UserId = recipientId,
                    Title = "New Document Upload",
                    Message = $"{Employee.FullName} uploaded a new document ({DocumentType}).",
                    TargetUrl = "/Employees?tab=documents",
                    IsRead = false,
                    CreatedAt = HRMS.Domain.Common.SriLankaTime.Now
                });
            }

            await _context.SaveChangesAsync();

            TempData["UploadSuccess"] = $"\"{UploadedFile.FileName}\" uploaded successfully and is pending HR review.";
            return RedirectToPage(new { Tab = "documents" });
        }

        public async Task<IActionResult> OnPostCancelDocumentAsync(int documentId)
        {
            await LoadAsync();
            if (Employee == null)
            {
                return Challenge();
            }

            var doc = await _context.EmployeeDocuments
                .FirstOrDefaultAsync(d => d.Id == documentId && d.EmployeeId == Employee.Id);

            if (doc == null)
            {
                TempData["UploadError"] = "Document not found.";
                return RedirectToPage(new { Tab = "documents" });
            }

            if (doc.Status != "Pending")
            {
                TempData["UploadError"] = "Only pending document requests can be cancelled.";
                return RedirectToPage(new { Tab = "documents" });
            }

            // Remove physical file from uploads if present
            if (!string.IsNullOrEmpty(doc.StoredFileName))
            {
                var filePath = Path.Combine(_env.WebRootPath, "uploads", "documents", doc.StoredFileName);
                if (System.IO.File.Exists(filePath))
                {
                    try { System.IO.File.Delete(filePath); } catch { /* ignore */ }
                }
            }

            _context.EmployeeDocuments.Remove(doc);
            await _context.SaveChangesAsync();

            TempData["UploadSuccess"] = $"Document request for \"{doc.FileName}\" has been cancelled.";
            return RedirectToPage(new { Tab = "documents" });
        }

        private async Task LoadAsync()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return;

            if (currentUser.EmployeeId.HasValue)
            {
                Employee = await _context.Employees
                    .Include(e => e.Department)
                    .Include(e => e.Designation)
                    .Include(e => e.Branch)
                    .Include(e => e.ReportingOfficer)
                    .FirstOrDefaultAsync(e => e.Id == currentUser.EmployeeId.Value);
            }

            if (Employee == null && !string.IsNullOrEmpty(currentUser.Email))
            {
                Employee = await _context.Employees
                    .Include(e => e.Department)
                    .Include(e => e.Designation)
                    .Include(e => e.Branch)
                    .Include(e => e.ReportingOfficer)
                    .FirstOrDefaultAsync(e => e.Email == currentUser.Email);
            }

            if (Employee == null && !string.IsNullOrEmpty(currentUser.UserName))
            {
                Employee = await _context.Employees
                    .Include(e => e.Department)
                    .Include(e => e.Designation)
                    .Include(e => e.Branch)
                    .Include(e => e.ReportingOfficer)
                    .FirstOrDefaultAsync(e => e.Email == currentUser.UserName);
            }

            if (Employee != null)
            {
                Documents = await _context.EmployeeDocuments
                    .Where(d => d.EmployeeId == Employee.Id)
                    .OrderByDescending(d => d.UploadedAt)
                    .ToListAsync();

                var salaries = await _context.PayrollSalaries
                    .Where(s => s.EmployeeId == Employee.Id)
                    .OrderByDescending(s => s.EffectiveDate)
                    .ThenByDescending(s => s.Id)
                    .ToListAsync();

                CurrentSalary = salaries.FirstOrDefault();
                SalaryHistory = salaries;
            }

            DesignationTitle = Employee?.Designation?.Title ?? string.Empty;

            if (string.IsNullOrWhiteSpace(DesignationTitle) && Employee?.DesignationId is int designationId && designationId > 0)
            {
                DesignationTitle = await _context.Designations
                    .Where(d => d.Id == designationId)
                    .Select(d => d.Title)
                    .FirstOrDefaultAsync() ?? string.Empty;
            }

            if (string.IsNullOrWhiteSpace(DesignationTitle) && !string.IsNullOrWhiteSpace(currentUser.Designation))
            {
                DesignationTitle = currentUser.Designation;
            }
        }
    }
}
