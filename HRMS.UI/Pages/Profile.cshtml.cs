using HRMS.Domain.Entities.Core;
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

        public Employee? Employee { get; set; }
        public List<EmployeeDocument> Documents { get; set; } = new();

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

            // Fetch HR / Admin users to notify
            var adminUsers = await _userManager.GetUsersInRoleAsync("Admin");
            var hrUsers = await _userManager.GetUsersInRoleAsync("HR Manager");
            var notifyUsers = adminUsers.Concat(hrUsers).Select(u => u.Id).Distinct();

            foreach (var hrId in notifyUsers)
            {
                _context.Notifications.Add(new Notification
                {
                    UserId = hrId,
                    Title = "New Document Upload",
                    Message = $"{Employee.FullName} uploaded a new document.",
                    TargetUrl = "/Employees?tab=documents",
                    IsRead = false,
                    CreatedAt = DateTime.Now
                });
            }

            await _context.SaveChangesAsync();

            TempData["UploadSuccess"] = $"\"{UploadedFile.FileName}\" uploaded successfully and is pending HR review.";
            return RedirectToPage(new { Tab = "documents" });
        }

        private async Task LoadAsync()
        {
            var email = User.Identity?.Name;
            if (string.IsNullOrEmpty(email)) return;

            Employee = await _context.Employees
                .Include(e => e.Department)
                .Include(e => e.Designation)
                .Include(e => e.Branch)
                .Include(e => e.ReportingOfficer)
                .FirstOrDefaultAsync(e => e.Email == email);

            if (Employee != null)
            {
                Documents = await _context.EmployeeDocuments
                    .Where(d => d.EmployeeId == Employee.Id)
                    .OrderByDescending(d => d.UploadedAt)
                    .ToListAsync();
            }
        }
    }
}
