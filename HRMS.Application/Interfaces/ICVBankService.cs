using System.Collections.Generic;
using System.Threading.Tasks;
using HRMS.Domain.Entities;
using HRMS.Domain.Entities.Recruitment;

namespace HRMS.Application.Interfaces
{
    public interface ICVBankService
    {
        // ── Candidates & CVs ──
        Task<IEnumerable<CVBank>> GetAllCVsAsync();
        Task<CVBank?> GetCVByIdAsync(int id);
        Task AddCVAsync(CVBank cv);
        Task DeleteCVAsync(int id);
        Task<IEnumerable<CVBank>> GetCandidatesByJobOpeningIdAsync(int jobOpeningId);

        // ── Job Openings ──
        Task<IEnumerable<JobOpening>> GetAllJobOpeningsAsync(bool activeOnly = false);
        Task<JobOpening?> GetJobOpeningByIdAsync(int id);
        Task<JobOpening?> GetJobOpeningByCodeAsync(string jobCode);
        Task AddJobOpeningAsync(JobOpening job);
        Task UpdateJobOpeningAsync(JobOpening job);
        Task DeleteJobOpeningAsync(int id);

        // ── Adaptive Scoring Engine ──
        int CalculateAdaptiveScore(CVBank cv, JobOpening? job);
    }
}
