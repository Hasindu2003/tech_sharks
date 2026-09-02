using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HRMS.Application.Interfaces;
using HRMS.Domain.Entities;
using HRMS.Domain.Entities.Recruitment;
using HRMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Application.Services
{
    public class CVBankService : ICVBankService
    {
        private readonly ApplicationDbContext _context;

        public CVBankService(ApplicationDbContext context)
        {
            _context = context;
        }

        // ══════════════════════════════════════════════════════════
        // Candidates & CVs
        // ══════════════════════════════════════════════════════════

        public async Task<IEnumerable<CVBank>> GetAllCVsAsync()
        {
            return await _context.CVBanks
                .Include(c => c.JobOpening)
                    .ThenInclude(j => j!.Department)
                .Include(c => c.JobOpening)
                    .ThenInclude(j => j!.Branch)
                .OrderByDescending(c => c.UploadedDate)
                .ToListAsync();
        }

        public async Task<CVBank?> GetCVByIdAsync(int id)
        {
            return await _context.CVBanks
                .Include(c => c.JobOpening)
                    .ThenInclude(j => j!.Department)
                .Include(c => c.JobOpening)
                    .ThenInclude(j => j!.Branch)
                .FirstOrDefaultAsync(c => c.Id == id);
        }

        public async Task AddCVAsync(CVBank cv)
        {
            JobOpening? job = null;
            if (cv.JobOpeningId.HasValue && cv.JobOpeningId.Value > 0)
            {
                job = await _context.JobOpenings.FindAsync(cv.JobOpeningId.Value);
            }

            cv.ExperienceScore = CalculateAdaptiveScore(cv, job);
            _context.CVBanks.Add(cv);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteCVAsync(int id)
        {
            var cv = await _context.CVBanks.FindAsync(id);
            if (cv != null)
            {
                _context.CVBanks.Remove(cv);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<IEnumerable<CVBank>> GetCandidatesByJobOpeningIdAsync(int jobOpeningId)
        {
            return await _context.CVBanks
                .Include(c => c.JobOpening)
                .Where(c => c.JobOpeningId == jobOpeningId)
                .OrderByDescending(c => c.ExperienceScore)
                .ThenByDescending(c => c.UploadedDate)
                .ToListAsync();
        }

        // ══════════════════════════════════════════════════════════
        // Job Openings
        // ══════════════════════════════════════════════════════════

        public async Task<IEnumerable<JobOpening>> GetAllJobOpeningsAsync(bool activeOnly = false)
        {
            var query = _context.JobOpenings
                .Include(j => j.Department)
                .Include(j => j.Branch)
                .Include(j => j.Applications)
                .AsQueryable();

            if (activeOnly)
            {
                query = query.Where(j => j.Status == "Open" && (!j.ClosingDate.HasValue || j.ClosingDate.Value.Date >= DateTime.Today));
            }

            return await query.OrderByDescending(j => j.CreatedDate).ToListAsync();
        }

        public async Task<JobOpening?> GetJobOpeningByIdAsync(int id)
        {
            return await _context.JobOpenings
                .Include(j => j.Department)
                .Include(j => j.Branch)
                .Include(j => j.Applications)
                .FirstOrDefaultAsync(j => j.Id == id);
        }

        public async Task<JobOpening?> GetJobOpeningByCodeAsync(string jobCode)
        {
            if (string.IsNullOrWhiteSpace(jobCode)) return null;
            return await _context.JobOpenings
                .Include(j => j.Department)
                .Include(j => j.Branch)
                .Include(j => j.Applications)
                .FirstOrDefaultAsync(j => j.JobCode != null && j.JobCode.ToLower() == jobCode.Trim().ToLower());
        }

        public async Task AddJobOpeningAsync(JobOpening job)
        {
            if (string.IsNullOrWhiteSpace(job.JobCode))
            {
                var year = DateTime.Now.Year;
                var count = await _context.JobOpenings.CountAsync(j => j.CreatedDate.Year == year) + 1;
                job.JobCode = $"JOB-{year}-{count:D3}";
            }

            _context.JobOpenings.Add(job);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateJobOpeningAsync(JobOpening job)
        {
            _context.JobOpenings.Update(job);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteJobOpeningAsync(int id)
        {
            var job = await _context.JobOpenings
                .Include(j => j.Applications)
                .FirstOrDefaultAsync(j => j.Id == id);

            if (job != null)
            {
                foreach (var app in job.Applications)
                {
                    app.JobOpeningId = null;
                }
                _context.JobOpenings.Remove(job);
                await _context.SaveChangesAsync();
            }
        }

        // ══════════════════════════════════════════════════════════
        // Adaptive Scoring Engine
        // ══════════════════════════════════════════════════════════

        public int CalculateAdaptiveScore(CVBank cv, JobOpening? job)
        {
            if (cv == null) return 0;

            // Fallback: If no job opening is targeted (general talent pool)
            if (job == null)
            {
                int baseScore = 0;
                int cappedYears = Math.Max(0, Math.Min(10, cv.ExperienceYears));
                baseScore += (cappedYears * 5); // Max 50 pts
                if (cv.HasDegree) baseScore += 25;
                if (cv.HasMasters) baseScore += 25;
                return Math.Min(100, baseScore);
            }

            double totalScore = 0;

            // 1. Experience Benchmark (Weight: 40 points)
            int minExp = Math.Max(0, job.MinimumExperienceYears);
            int candExp = Math.Max(0, cv.ExperienceYears);

            if (minExp == 0)
            {
                totalScore += 25 + (Math.Min(candExp, 5) * 3);
            }
            else if (candExp >= minExp)
            {
                int surplusYears = candExp - minExp;
                totalScore += 25 + (Math.Min(surplusYears, 5) * 3);
            }
            else
            {
                totalScore += ((double)candExp / minExp) * 25.0;
            }

            // 2. Academic Qualification Benchmark (Weight: 35 points)
            var minEdu = (job.MinimumEducationLevel ?? "None").Trim();
            if (minEdu.Equals("Masters", StringComparison.OrdinalIgnoreCase))
            {
                if (cv.HasMasters) totalScore += 35;
                else if (cv.HasDegree) totalScore += 20;
                else totalScore += 0;
            }
            else if (minEdu.Equals("Degree", StringComparison.OrdinalIgnoreCase))
            {
                if (cv.HasMasters) totalScore += 35;
                else if (cv.HasDegree) totalScore += 30;
                else totalScore += 5;
            }
            else
            {
                if (cv.HasMasters) totalScore += 35;
                else if (cv.HasDegree) totalScore += 30;
                else totalScore += 20;
            }

            // 3. Skills & Competency Matching (Weight: 25 points)
            if (!string.IsNullOrWhiteSpace(job.RequiredSkills))
            {
                var reqSkills = job.RequiredSkills
                    .Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Select(s => s.Trim().ToLowerInvariant())
                    .Distinct()
                    .ToList();

                if (reqSkills.Any() && !string.IsNullOrWhiteSpace(cv.Skills) && cv.Skills != "Not Specified" && cv.Skills != "None")
                {
                    var candSkills = cv.Skills
                        .Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .Where(s => !string.IsNullOrWhiteSpace(s))
                        .Select(s => s.Trim().ToLowerInvariant())
                        .Distinct()
                        .ToList();

                    int matchedCount = 0;
                    foreach (var req in reqSkills)
                    {
                        if (candSkills.Any(cs => cs.Equals(req, StringComparison.OrdinalIgnoreCase) 
                                              || cs.Contains(req, StringComparison.OrdinalIgnoreCase) 
                                              || req.Contains(cs, StringComparison.OrdinalIgnoreCase)))
                        {
                            matchedCount++;
                        }
                    }

                    double skillRatio = (double)matchedCount / reqSkills.Count;
                    totalScore += (skillRatio * 25.0);
                }
                else if (reqSkills.Any())
                {
                    totalScore += 0;
                }
                else
                {
                    totalScore += 15;
                }
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(cv.Skills) && cv.Skills != "Not Specified" && cv.Skills != "None")
                {
                    totalScore += 25;
                }
                else
                {
                    totalScore += 15;
                }
            }

            return Math.Max(0, Math.Min(100, (int)Math.Round(totalScore)));
        }
    }
}
