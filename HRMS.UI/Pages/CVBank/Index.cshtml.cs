using HRMS.Application.Interfaces;
using HRMS.Domain.Entities;
using HRMS.Domain.Entities.Recruitment;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using CVBankEntity = HRMS.Domain.Entities.CVBank;

namespace HRMS.UI.Pages.CVBank
{
    [Authorize(Roles = "HR Manager, HR Officer, Area Manager, Branch Manager")]
    public class IndexModel : PageModel
    {
        private readonly ICVBankService _cvService;
        private readonly IWebHostEnvironment _environment;
        private readonly IConfiguration _configuration;

        public IndexModel(ICVBankService cvService, IWebHostEnvironment environment, IConfiguration configuration)
        {
            _cvService = cvService;
            _environment = environment;
            _configuration = configuration;
        }

        public List<CVBankEntity> CVList { get; set; } = new();
        public List<JobOpening> JobOpenings { get; set; } = new();
        public List<string> PositionOptions { get; set; } = new();
        public Dictionary<string, int> PositionCounts { get; set; } = new();

        [BindProperty(SupportsGet = true)]
        public string? Tab { get; set; } = "jobs"; // "jobs" or "candidates"

        [BindProperty(SupportsGet = true)]
        public string? Search { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? Position { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? JobId { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? MinScore { get; set; }

        public int ActiveOpeningsCount { get; set; }
        public int TotalCandidates { get; set; }
        public int HighRankedCount { get; set; }
        public int TotalPositionsCount { get; set; }
        public double AverageScore { get; set; }
        public string PublicBaseUrl { get; set; } = string.Empty;

        public async Task OnGetAsync()
        {
            if (string.IsNullOrWhiteSpace(Tab))
            {
                Tab = JobId.HasValue ? "candidates" : "jobs";
            }

            // 1. Load Job Openings
            var allJobs = await _cvService.GetAllJobOpeningsAsync() ?? new List<JobOpening>();
            JobOpenings = allJobs.ToList();
            ActiveOpeningsCount = JobOpenings.Count(j => j.Status == "Open" && (!j.ClosingDate.HasValue || j.ClosingDate.Value.Date >= DateTime.Today));

            // 2. Load Candidates & CVs
            var allCVs = (await _cvService.GetAllCVsAsync()) ?? Enumerable.Empty<CVBankEntity>();
            var cvList = allCVs.ToList();

            TotalCandidates = cvList.Count;
            HighRankedCount = cvList.Count(c => c.ExperienceScore >= 75);
            AverageScore = cvList.Any() ? Math.Round(cvList.Average(c => c.ExperienceScore), 1) : 0;
            
            PositionOptions = cvList.Select(c => c.AppliedPosition).Where(p => !string.IsNullOrEmpty(p)).Distinct().OrderBy(p => p).ToList();
            TotalPositionsCount = PositionOptions.Count;

            PositionCounts = cvList
                .Where(c => !string.IsNullOrEmpty(c.AppliedPosition))
                .GroupBy(c => c.AppliedPosition)
                .ToDictionary(g => g.Key, g => g.Count());

            var filtered = cvList.AsQueryable();

            if (JobId.HasValue && JobId.Value > 0)
            {
                filtered = filtered.Where(c => c.JobOpeningId == JobId.Value);
            }

            if (!string.IsNullOrWhiteSpace(Search))
            {
                var term = Search.Trim().ToLower();
                filtered = filtered.Where(c => 
                    (c.CandidateName != null && c.CandidateName.ToLower().Contains(term)) ||
                    (c.Email != null && c.Email.ToLower().Contains(term)) ||
                    (c.ContactNumber != null && c.ContactNumber.ToLower().Contains(term)) ||
                    (c.Skills != null && c.Skills.ToLower().Contains(term)) ||
                    (c.AppliedPosition != null && c.AppliedPosition.ToLower().Contains(term)) ||
                    (c.JobOpening != null && c.JobOpening.JobCode != null && c.JobOpening.JobCode.ToLower().Contains(term))
                );
            }

            if (!string.IsNullOrWhiteSpace(Position) && Position != "All")
            {
                filtered = filtered.Where(c => c.AppliedPosition.Equals(Position, StringComparison.OrdinalIgnoreCase));
            }

            if (MinScore.HasValue && MinScore.Value > 0)
            {
                filtered = filtered.Where(c => c.ExperienceScore >= MinScore.Value);
            }

            CVList = filtered.OrderByDescending(c => c.ExperienceScore).ThenByDescending(c => c.UploadedDate).ToList();

            PublicBaseUrl = ResolvePublicBaseUrl();
        }

        private string ResolvePublicBaseUrl()
        {
            var configuredUrl = _configuration["CareersPortalUrl"];
            if (!string.IsNullOrWhiteSpace(configuredUrl))
            {
                return configuredUrl.TrimEnd('/');
            }

            var host = Request.Headers["X-Forwarded-Host"].FirstOrDefault() ?? Request.Host.Value;
            var scheme = Request.Headers["X-Forwarded-Proto"].FirstOrDefault() ?? Request.Scheme;

            if (host.Contains("azurewebsites.net", StringComparison.OrdinalIgnoreCase))
            {
                scheme = "https";
            }

            return $"{scheme}://{host}";
        }

        public async Task<IActionResult> OnPostToggleJobStatusAsync(int id)
        {
            var job = await _cvService.GetJobOpeningByIdAsync(id);
            if (job != null)
            {
                job.Status = job.Status == "Open" ? "Closed" : "Open";
                await _cvService.UpdateJobOpeningAsync(job);
                TempData["SuccessMessage"] = $"Job Opening '{job.Title}' status was updated to '{job.Status}'.";
            }

            return RedirectToPage(new { Tab = "jobs" });
        }

        public async Task<IActionResult> OnPostDeleteJobAsync(int id)
        {
            var job = await _cvService.GetJobOpeningByIdAsync(id);
            if (job != null)
            {
                await _cvService.DeleteJobOpeningAsync(id);
                TempData["SuccessMessage"] = $"Job Opening '{job.Title}' (Ref: {job.JobCode}) was successfully deleted.";
            }

            return RedirectToPage(new { Tab = "jobs" });
        }

        public async Task<IActionResult> OnPostDeleteAsync(int id)
        {
            var cv = await _cvService.GetCVByIdAsync(id);
            if (cv != null)
            {
                if (!string.IsNullOrEmpty(cv.CVFilePath))
                {
                    try
                    {
                        var fullPath = Path.Combine(_environment.WebRootPath, cv.CVFilePath.TrimStart('/').TrimStart('\\'));
                        if (System.IO.File.Exists(fullPath))
                        {
                            System.IO.File.Delete(fullPath);
                        }
                    }
                    catch { }
                }

                await _cvService.DeleteCVAsync(id);
                TempData["SuccessMessage"] = $"Candidate '{cv.CandidateName}' was successfully deleted.";
            }

            return RedirectToPage(new { Tab = "candidates", Search, Position, MinScore, JobId });
        }
    }
}
