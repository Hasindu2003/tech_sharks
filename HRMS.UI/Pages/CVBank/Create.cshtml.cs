using HRMS.Application.Interfaces;
using HRMS.Domain.Entities;
using HRMS.Domain.Entities.Recruitment;
using HRMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace HRMS.UI.Pages.CVBank
{
    [Authorize(Roles = "HR Manager, HR Officer, Area Manager, Branch Manager")]
    public class CreateModel : PageModel
    {
        private readonly ICVBankService _cvService;
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;

        public CreateModel(ICVBankService cvService, ApplicationDbContext context, IWebHostEnvironment environment)
        {
            _cvService = cvService;
            _context = context;
            _environment = environment;
        }

        [BindProperty]
        public HRMS.Domain.Entities.CVBank CVInput { get; set; } = new HRMS.Domain.Entities.CVBank();

        [BindProperty]
        public IFormFile? UploadedCV { get; set; }

        public List<string> PositionOptions { get; set; } = new();
        public List<JobOpening> JobOpenings { get; set; } = new();

        public async Task OnGetAsync()
        {
            await LoadPositionsAsync();
        }

        private async Task LoadPositionsAsync()
        {
            JobOpenings = (await _cvService.GetAllJobOpeningsAsync(activeOnly: true)).ToList();

            var defaultRoles = new List<string>
            {
                "Accountant",
                "Credit Officer",
                "Investment Analyst",
                "Branch Manager",
                "Finance Assistant",
                "HR Officer",
                "Operations Executive",
                "IT Support Specialist",
                "Internal Audit Officer",
                "Customer Service Officer"
            };

            try
            {
                var dbTitles = await _context.Designations
                    .Where(d => !string.IsNullOrEmpty(d.Title))
                    .Select(d => d.Title)
                    .Distinct()
                    .ToListAsync();

                var activeJobTitles = JobOpenings.Select(j => j.Title).ToList();

                PositionOptions = activeJobTitles
                    .Union(dbTitles)
                    .Union(defaultRoles)
                    .OrderBy(p => p)
                    .ToList();
            }
            catch
            {
                PositionOptions = defaultRoles.OrderBy(p => p).ToList();
            }
        }

        public async Task<IActionResult> OnPostAsync()
        {
            ModelState.Remove("CVInput.CVFilePath");
            ModelState.Remove("CVInput.ExperienceScore");

            // 1. Validate Candidate Name
            if (string.IsNullOrWhiteSpace(CVInput.CandidateName))
            {
                ModelState.AddModelError("CVInput.CandidateName", "Candidate full name is required.");
            }
            else
            {
                var name = CVInput.CandidateName.Trim();
                if (name.Length < 2)
                {
                    ModelState.AddModelError("CVInput.CandidateName", "Candidate name must be at least 2 characters.");
                }
                else if (name.Length > 100)
                {
                    ModelState.AddModelError("CVInput.CandidateName", "Candidate name cannot exceed 100 characters.");
                }
                else if (!Regex.IsMatch(name, @"^[a-zA-Z\s\.\,\'\-]+$"))
                {
                    ModelState.AddModelError("CVInput.CandidateName", "Candidate name can only contain letters, spaces, dots, and hyphens.");
                }
            }

            // 2. Validate Email Address
            if (string.IsNullOrWhiteSpace(CVInput.Email))
            {
                ModelState.AddModelError("CVInput.Email", "Email address is required.");
            }
            else
            {
                var email = CVInput.Email.Trim();
                if (email.Length > 100)
                {
                    ModelState.AddModelError("CVInput.Email", "Email address cannot exceed 100 characters.");
                }
                else if (!Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
                {
                    ModelState.AddModelError("CVInput.Email", "Please enter a valid email address (e.g. name@example.com).");
                }
            }

            // 3. Validate Contact Number
            if (!string.IsNullOrWhiteSpace(CVInput.ContactNumber) && CVInput.ContactNumber != "N/A")
            {
                var phone = CVInput.ContactNumber.Trim();
                if (!Regex.IsMatch(phone, @"^\+?[0-9\s\-]{9,15}$"))
                {
                    ModelState.AddModelError("CVInput.ContactNumber", "Please enter a valid phone number (9-15 digits, e.g. 0771234567 or +94 77 123 4567).");
                }
            }

            // 4. Validate Applied Position
            if (string.IsNullOrWhiteSpace(CVInput.AppliedPosition) || CVInput.AppliedPosition == "-- Select Position --")
            {
                ModelState.AddModelError("CVInput.AppliedPosition", "Please select an applied position.");
            }

            // 5. Validate Experience Years
            if (CVInput.ExperienceYears < 0 || CVInput.ExperienceYears > 50)
            {
                ModelState.AddModelError("CVInput.ExperienceYears", "Experience must be between 0 and 50 years.");
            }

            // 6. Validate Uploaded Document (Mandatory)
            if (UploadedCV == null || UploadedCV.Length == 0)
            {
                ModelState.AddModelError("UploadedCV", "Please attach candidate Resume / CV document (PDF or Word).");
            }
            else
            {
                var allowedExtensions = new[] { ".pdf", ".docx", ".doc" };
                var ext = Path.GetExtension(UploadedCV.FileName).ToLowerInvariant();
                if (!allowedExtensions.Contains(ext))
                {
                    ModelState.AddModelError("UploadedCV", "Only PDF, DOC, or DOCX documents are allowed.");
                }
                else if (UploadedCV.Length > 15 * 1024 * 1024)
                {
                    ModelState.AddModelError("UploadedCV", "File size cannot exceed 15MB.");
                }
            }

            if (!ModelState.IsValid)
            {
                await LoadPositionsAsync();
                return Page();
            }

            try
            {
                if (CVInput.JobOpeningId.HasValue && CVInput.JobOpeningId.Value > 0)
                {
                    var job = await _cvService.GetJobOpeningByIdAsync(CVInput.JobOpeningId.Value);
                    if (job != null)
                    {
                        CVInput.AppliedPosition = job.Title;
                    }
                }

                if (UploadedCV != null && UploadedCV.Length > 0)
                {
                    var ext = Path.GetExtension(UploadedCV.FileName).ToLowerInvariant();
                    string uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "cvs");
                    if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

                    string safeFileName = Path.GetFileNameWithoutExtension(UploadedCV.FileName);
                    safeFileName = string.Join("_", safeFileName.Split(Path.GetInvalidFileNameChars()));
                    string uniqueFileName = $"{Guid.NewGuid():N}_{safeFileName}{ext}";
                    string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await UploadedCV.CopyToAsync(fileStream);
                    }
                    CVInput.CVFilePath = "/uploads/cvs/" + uniqueFileName;
                }

                CVInput.UploadedDate = DateTime.Now;
                CVInput.CandidateName = CVInput.CandidateName.Trim();
                CVInput.Email = CVInput.Email.Trim().ToLowerInvariant();
                if (string.IsNullOrWhiteSpace(CVInput.Skills)) CVInput.Skills = "Not Specified";
                else CVInput.Skills = CVInput.Skills.Trim();
                if (string.IsNullOrWhiteSpace(CVInput.ContactNumber)) CVInput.ContactNumber = "N/A";
                else CVInput.ContactNumber = CVInput.ContactNumber.Trim();

                // AddCVAsync computes the adaptive score based on linked JobOpening
                await _cvService.AddCVAsync(CVInput);
                TempData["SuccessMessage"] = $"Candidate '{CVInput.CandidateName}' successfully registered with Score {CVInput.ExperienceScore}/100.";

                return RedirectToPage("Index", new { Tab = "candidates" });
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, "An error occurred while adding candidate: " + ex.Message);
                await LoadPositionsAsync();
                return Page();
            }
        }
    }
}
