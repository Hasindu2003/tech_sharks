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

using CVBankEntity = HRMS.Domain.Entities.CVBank;

namespace HRMS.UI.Pages
{
    [AllowAnonymous]
    public class ApplyModel : PageModel
    {
        private readonly ICVBankService _cvService;
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;

        public ApplyModel(ICVBankService cvService, ApplicationDbContext context, IWebHostEnvironment environment)
        {
            _cvService = cvService;
            _context = context;
            _environment = environment;
        }

        [BindProperty]
        public CVBankEntity CVInput { get; set; } = new CVBankEntity();

        [BindProperty]
        public IFormFile? UploadedCV { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? JobId { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? JobCode { get; set; }

        public JobOpening? TargetJob { get; set; }
        public bool IsTargetJobClosed { get; set; }
        public bool IsTargetJobNotFound { get; set; }
        public string? ClosedJobTitle { get; set; }
        public string? ClosedJobCode { get; set; }

        public List<string> PositionOptions { get; set; } = new();
        public List<JobOpening> ActiveOpenings { get; set; } = new();

        public bool IsSubmitted { get; set; }
        public string SubmittedCandidateName { get; set; } = string.Empty;
        public string SubmittedPosition { get; set; } = string.Empty;
        public string? SubmittedJobCode { get; set; }
        public int SubmittedScore { get; set; }

        public async Task OnGetAsync()
        {
            await LoadPositionsAndJobAsync();
        }

        private async Task LoadPositionsAndJobAsync()
        {
            bool wasTargeted = false;

            // 1. Fetch Target Job Opening if specified in URL query
            if (JobId.HasValue && JobId.Value > 0)
            {
                wasTargeted = true;
                TargetJob = await _cvService.GetJobOpeningByIdAsync(JobId.Value);
            }
            else if (!string.IsNullOrWhiteSpace(JobCode))
            {
                wasTargeted = true;
                TargetJob = await _cvService.GetJobOpeningByCodeAsync(JobCode);
            }

            if (wasTargeted)
            {
                if (TargetJob == null)
                {
                    IsTargetJobNotFound = true;
                }
                else
                {
                    bool isExpired = TargetJob.ClosingDate.HasValue && TargetJob.ClosingDate.Value.Date < DateTime.Today;
                    if (TargetJob.Status == "Closed" || isExpired)
                    {
                        IsTargetJobClosed = true;
                        ClosedJobTitle = TargetJob.Title;
                        ClosedJobCode = TargetJob.JobCode;
                        TargetJob = null; // Prevent binding to closed job
                    }
                    else
                    {
                        CVInput.JobOpeningId = TargetJob.Id;
                        CVInput.AppliedPosition = TargetJob.Title;
                    }
                }
            }

            // 2. Fetch Active Openings for general dropdown
            ActiveOpenings = (await _cvService.GetAllJobOpeningsAsync(activeOnly: true)).ToList();

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

                var activeJobTitles = ActiveOpenings.Select(j => j.Title).ToList();

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

            if (CVInput.JobOpeningId.HasValue && CVInput.JobOpeningId.Value > 0)
            {
                TargetJob = await _cvService.GetJobOpeningByIdAsync(CVInput.JobOpeningId.Value);
                if (TargetJob == null)
                {
                    IsTargetJobNotFound = true;
                    await LoadPositionsAndJobAsync();
                    return Page();
                }

                bool isExpired = TargetJob.ClosingDate.HasValue && TargetJob.ClosingDate.Value.Date < DateTime.Today;
                if (TargetJob.Status == "Closed" || isExpired)
                {
                    IsTargetJobClosed = true;
                    ClosedJobTitle = TargetJob.Title;
                    ClosedJobCode = TargetJob.JobCode;
                    TargetJob = null;
                    await LoadPositionsAndJobAsync();
                    return Page();
                }

                CVInput.AppliedPosition = TargetJob.Title;
                ModelState.Remove("CVInput.AppliedPosition");
            }

            // 1. Validate Candidate Name
            if (string.IsNullOrWhiteSpace(CVInput.CandidateName))
            {
                ModelState.AddModelError("CVInput.CandidateName", "Full Name is required.");
            }
            else
            {
                var name = CVInput.CandidateName.Trim();
                if (name.Length < 2)
                {
                    ModelState.AddModelError("CVInput.CandidateName", "Full Name must be at least 2 characters.");
                }
                else if (name.Length > 100)
                {
                    ModelState.AddModelError("CVInput.CandidateName", "Full Name cannot exceed 100 characters.");
                }
                else if (!Regex.IsMatch(name, @"^[a-zA-Z\s\.\,\'\-]+$"))
                {
                    ModelState.AddModelError("CVInput.CandidateName", "Full Name can only contain letters, spaces, dots, and hyphens.");
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
            if (string.IsNullOrWhiteSpace(CVInput.ContactNumber))
            {
                ModelState.AddModelError("CVInput.ContactNumber", "Contact number is required.");
            }
            else
            {
                var phone = CVInput.ContactNumber.Trim();
                if (!Regex.IsMatch(phone, @"^\+?[0-9\s\-]{9,15}$"))
                {
                    ModelState.AddModelError("CVInput.ContactNumber", "Please enter a valid phone number (9-15 digits, e.g. 0771234567 or +94 77 123 4567).");
                }
            }

            // 4. Validate Applied Position
            if (string.IsNullOrWhiteSpace(CVInput.AppliedPosition) || CVInput.AppliedPosition == "-- Choose Position --")
            {
                ModelState.AddModelError("CVInput.AppliedPosition", "Please select the position you are applying for.");
            }

            // 5. Validate Experience Years
            if (CVInput.ExperienceYears < 0 || CVInput.ExperienceYears > 50)
            {
                ModelState.AddModelError("CVInput.ExperienceYears", "Experience must be a positive number between 0 and 50 years.");
            }

            // 6. Validate Resume File
            if (UploadedCV == null || UploadedCV.Length == 0)
            {
                ModelState.AddModelError("UploadedCV", "Please attach your Resume / CV file (PDF or Word document).");
            }
            else
            {
                var allowedExtensions = new[] { ".pdf", ".docx", ".doc" };
                var ext = Path.GetExtension(UploadedCV.FileName).ToLowerInvariant();
                if (!allowedExtensions.Contains(ext))
                {
                    ModelState.AddModelError("UploadedCV", "Invalid file format. Only PDF, DOC, or DOCX documents are accepted.");
                }
                else if (UploadedCV.Length > 15 * 1024 * 1024)
                {
                    ModelState.AddModelError("UploadedCV", "File size is too large. Maximum allowed size is 15MB.");
                }
            }

            if (!ModelState.IsValid)
            {
                await LoadPositionsAndJobAsync();
                return Page();
            }

            try
            {
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
                CVInput.CandidateName = CVInput.CandidateName?.Trim() ?? string.Empty;
                CVInput.Email = CVInput.Email?.Trim().ToLowerInvariant() ?? string.Empty;
                CVInput.ContactNumber = CVInput.ContactNumber?.Trim() ?? "N/A";
                if (string.IsNullOrWhiteSpace(CVInput.Skills)) CVInput.Skills = "Not Specified";
                else CVInput.Skills = CVInput.Skills.Trim();

                // AddCVAsync automatically computes the Adaptive Score based on TargetJob benchmarks!
                await _cvService.AddCVAsync(CVInput);

                IsSubmitted = true;
                SubmittedCandidateName = CVInput.CandidateName;
                SubmittedPosition = CVInput.AppliedPosition;
                SubmittedJobCode = TargetJob?.JobCode;
                SubmittedScore = CVInput.ExperienceScore;

                return Page();
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, "An error occurred while submitting your application: " + ex.Message);
                await LoadPositionsAndJobAsync();
                return Page();
            }
        }
    }
}
