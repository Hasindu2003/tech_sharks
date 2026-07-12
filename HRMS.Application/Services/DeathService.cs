using HRMS.Domain.Entities.Death;
using HRMS.Domain.Entities.Transfer;
using HRMS.Domain.Entities.Core;
using HRMS.Infrastructure.Identity;
using HRMS.Infrastructure.Persistence;
using HRMS.Application.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Application.Services
{
    public interface IDeathService
    {
        Task<DeathRequestViewModel?> GetByIdAsync(int id);
        Task<List<DeathRequestViewModel>> GetAllPendingForBMAsync(string branch);
        Task<List<DeathRequestViewModel>> GetAllPendingForAMAsync();
        Task<List<DeathRequestViewModel>> GetAllPendingForHRAsync();
        
        Task<int> SubmitRequestAsync(DeathRequestViewModel model, List<IFormFile> documents, string initiatedByEmail);

        Task<bool> BMApproveAsync(int id, string comments, string bmEmail);
        Task<bool> BMRejectAsync(int id, string comments, string bmEmail);

        Task<bool> AMApproveAsync(int id, string comments, string amEmail);
        Task<bool> AMRejectAsync(int id, string comments, string amEmail);

        Task<bool> HRManagerApproveAsync(int id, string comments, string hrEmail);
        Task<bool> HRManagerRejectAsync(int id, string comments, string hrEmail);

        Task<(byte[] Content, string ContentType, string FileName)?> DownloadDocumentAsync(int documentId);
        
        Task<(bool Success, string ErrorMessage)> ProcessClosureAsync(int id, string hrEmail, UserManager<ApplicationUser> userManager);
    }

    /// <summary>
    /// Service responsible for managing the process following an employee's death.
    /// Handles document submission, multi-stage approvals, and final account/payroll closure.
    /// </summary>
    public class DeathService : IDeathService
    {
        private readonly ApplicationDbContext _context;
        private readonly INotificationService _notificationService;

        public DeathService(ApplicationDbContext context, INotificationService notificationService)
        {
            _context = context;
            _notificationService = notificationService;
        }

        public async Task<DeathRequestViewModel?> GetByIdAsync(int id)
        {
            var req = await _context.DeathRequests
                .Include(r => r.Documents)
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == id);

            if (req == null) return null;

            return new DeathRequestViewModel
            {
                Id = req.Id,
                EmployeeName = req.EmployeeName,
                EpfNumber = req.EpfNumber,
                EmployeeEmail = req.EmployeeEmail,
                Branch = req.Branch,
                Department = req.Department,
                Designation = req.Designation,
                DateOfDeath = req.DateOfDeath,
                NatureOfDeath = req.NatureOfDeath,
                NomineeName = req.NomineeName,
                NomineeRelation = req.NomineeRelation,
                NomineeContact = req.NomineeContact,
                AdditionalRemarks = req.AdditionalRemarks,
                HasOutstandingLoans = req.HasOutstandingLoans,
                IsLoanGuarantor = req.IsLoanGuarantor,
                ObligationDetails = req.ObligationDetails,
                Status = req.Status,
                InitiatedBy = req.InitiatedBy,
                CreatedDate = req.CreatedDate,
                LastModifiedDate = req.LastModifiedDate,
                BMReview = req.BMReview,
                BMReviewDate = req.BMReviewDate,
                BMComments = req.BMComments,
                AMReview = req.AMReview,
                AMReviewDate = req.AMReviewDate,
                AMComments = req.AMComments,
                HRReview = req.HRReview,
                HRReviewDate = req.HRReviewDate,
                HRComments = req.HRComments,
                AccountDeactivated = req.AccountDeactivated,
                PayrollStopped = req.PayrollStopped,
                FinanceClearanceTriggered = req.FinanceClearanceTriggered,
                DocumentCount = req.Documents.Count,
                Documents = req.Documents.Select(d => new DeathDocumentViewModel
                {
                    Id = d.Id,
                    FileName = d.FileName,
                    ContentType = d.ContentType,
                    DocumentType = d.DocumentType,
                    UploadedDate = d.UploadedDate
                }).ToList()
            };
        }

        public async Task<List<DeathRequestViewModel>> GetAllPendingForBMAsync(string branch)
        {
            var branchLower = branch.Trim().ToLower();
            var data = await _context.DeathRequests
                .Where(r => r.Status == DeathRequestStatus.SubmittedForApproval && 
                            r.Branch.Trim().ToLower() == branchLower)
                .OrderBy(r => r.CreatedDate)
                .ToListAsync();
            return MapToVMList(data);
        }

        public async Task<List<DeathRequestViewModel>> GetAllPendingForAMAsync()
        {
            var data = await _context.DeathRequests
                .Where(r => r.Status == DeathRequestStatus.BMApproved)
                .OrderBy(r => r.CreatedDate)
                .ToListAsync();
            return MapToVMList(data);
        }

        public async Task<List<DeathRequestViewModel>> GetAllPendingForHRAsync()
        {
            var data = await _context.DeathRequests
                .Where(r => r.Status == DeathRequestStatus.AMApproved || r.Status == DeathRequestStatus.HRApproved)
                .OrderByDescending(r => r.CreatedDate)
                .ToListAsync();
            return MapToVMList(data);
        }

        /// <summary>
        /// Submits a new death request with supporting documentation.
        /// Initiated by a manager and moves the request to the BM review queue.
        /// </summary>
        public async Task<int> SubmitRequestAsync(DeathRequestViewModel model, List<IFormFile> documents, string initiatedByEmail)
        {
            var entity = new DeathRequest
            {
                EmployeeName = model.EmployeeName,
                EpfNumber = model.EpfNumber,
                EmployeeEmail = model.EmployeeEmail,
                Branch = model.Branch,
                Department = model.Department,
                Designation = model.Designation,
                DateOfDeath = model.DateOfDeath,
                NatureOfDeath = model.NatureOfDeath,
                NomineeName = model.NomineeName,
                NomineeRelation = model.NomineeRelation,
                NomineeContact = model.NomineeContact,
                AdditionalRemarks = model.AdditionalRemarks,
                HasOutstandingLoans = model.HasOutstandingLoans,
                IsLoanGuarantor = model.IsLoanGuarantor,
                ObligationDetails = model.ObligationDetails,
                Status = DeathRequestStatus.SubmittedForApproval,
                InitiatedBy = initiatedByEmail,
                CreatedDate = DateTime.UtcNow,
                LastModifiedDate = DateTime.UtcNow
            };

            foreach (var file in documents)
            {
                using var ms = new MemoryStream();
                await file.CopyToAsync(ms);
                entity.Documents.Add(new DeathDocument
                {
                    FileName = file.FileName,
                    ContentType = file.ContentType,
                    DocumentType = "Death Certificate/Proof",
                    Content = ms.ToArray(),
                    UploadedDate = DateTime.UtcNow
                });
            }

            _context.DeathRequests.Add(entity);
            await _context.SaveChangesAsync();

            // Notify BM
            await _notificationService.CreateNotificationAsync(
                "BM_ROLE", // Simplified logic for role broadcast
                "New Employee Death Request",
                $"A death request for {entity.EmployeeName} requires your review.",
                CoreNotificationType.Info,
                entity.Id,
                "/Separation/Dashboard"
            );

            return entity.Id;
        }

        /// <summary>
        /// Processes the Branch Manager's review of the death claim.
        /// </summary>
        public async Task<bool> BMApproveAsync(int id, string comments, string bmEmail)
        {
            var req = await _context.DeathRequests.FindAsync(id);
            if (req == null || req.Status != DeathRequestStatus.SubmittedForApproval) return false;

            req.Status = DeathRequestStatus.BMApproved;
            req.BMReview = "Approved";
            req.BMReviewDate = DateTime.UtcNow;
            req.BMComments = comments;
            req.BMEmail = bmEmail;
            req.LastModifiedDate = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> BMRejectAsync(int id, string comments, string bmEmail)
        {
            var req = await _context.DeathRequests.FindAsync(id);
            if (req == null || req.Status != DeathRequestStatus.SubmittedForApproval) return false;

            req.Status = DeathRequestStatus.BMRejected;
            req.BMReview = "Rejected";
            req.BMReviewDate = DateTime.UtcNow;
            req.BMComments = comments;
            req.BMEmail = bmEmail;
            req.LastModifiedDate = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            // notify HR or Initiator
            await _notificationService.CreateNotificationAsync(
                req.InitiatedBy,
                "Death Request Rejected",
                $"Your request for {req.EmployeeName} was rejected by BM.",
                CoreNotificationType.Rejected,
                req.Id,
                "/Separation/Dashboard"
            );

            return true;
        }

        public async Task<bool> AMApproveAsync(int id, string comments, string amEmail)
        {
            var req = await _context.DeathRequests.FindAsync(id);
            if (req == null || req.Status != DeathRequestStatus.BMApproved) return false;

            req.Status = DeathRequestStatus.AMApproved;
            req.AMReview = "Approved";
            req.AMReviewDate = DateTime.UtcNow;
            req.AMComments = comments;
            req.AMEmail = amEmail;
            req.LastModifiedDate = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> AMRejectAsync(int id, string comments, string amEmail)
        {
            var req = await _context.DeathRequests.FindAsync(id);
            if (req == null || req.Status != DeathRequestStatus.BMApproved) return false;

            req.Status = DeathRequestStatus.AMRejected;
            req.AMReview = "Rejected";
            req.AMReviewDate = DateTime.UtcNow;
            req.AMComments = comments;
            req.AMEmail = amEmail;
            req.LastModifiedDate = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return true;
        }

        /// <summary>
        /// Processes the final HR Manager review for the death claim.
        /// </summary>
        public async Task<bool> HRManagerApproveAsync(int id, string comments, string hrEmail)
        {
            var req = await _context.DeathRequests.FindAsync(id);
            if (req == null || req.Status != DeathRequestStatus.AMApproved) return false;

            req.Status = DeathRequestStatus.HRApproved;
            req.HRReview = "Approved";
            req.HRReviewDate = DateTime.UtcNow;
            req.HRComments = comments;
            req.HREmail = hrEmail;
            req.LastModifiedDate = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> HRManagerRejectAsync(int id, string comments, string hrEmail)
        {
            var req = await _context.DeathRequests.FindAsync(id);
            if (req == null || req.Status != DeathRequestStatus.AMApproved) return false;

            req.Status = DeathRequestStatus.HRRejected;
            req.HRReview = "Rejected";
            req.HRReviewDate = DateTime.UtcNow;
            req.HRComments = comments;
            req.HREmail = hrEmail;
            req.LastModifiedDate = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<(byte[] Content, string ContentType, string FileName)?> DownloadDocumentAsync(int documentId)
        {
            var doc = await _context.DeathDocuments.FindAsync(documentId);
            if (doc == null) return null;
            return (doc.Content, doc.ContentType, doc.FileName);
        }

        /// <summary>
        /// Finalizes the death claim by deactivating the employee account and stopping payroll.
        /// </summary>
        public async Task<(bool Success, string ErrorMessage)> ProcessClosureAsync(int id, string hrEmail, UserManager<ApplicationUser> userManager)
        {
            var req = await _context.DeathRequests.FindAsync(id);
            if (req == null || req.Status != DeathRequestStatus.HRApproved)
                return (false, "Request not found or not in HR Approved status.");

            var user = await userManager.FindByEmailAsync(req.EmployeeEmail);
            if (user != null)
            {
                user.LockoutEnabled = true;
                user.LockoutEnd = DateTimeOffset.MaxValue; // inactivate user account
                await userManager.UpdateAsync(user);
            }

            // Stop payroll, trigger finance manually by updating entity state
            req.AccountDeactivated = true;
            req.AccountDeactivatedDate = DateTime.UtcNow;
            req.AccountDeactivatedBy = hrEmail;
            req.PayrollStopped = true;
            req.FinanceClearanceTriggered = true;
            req.Status = DeathRequestStatus.Completed;

            await _context.SaveChangesAsync();

            // Emulate notifying nominee
            // _emailService.SendEmail(req.NomineeContact, "...", "...") etc.

            return (true, string.Empty);
        }

        private List<DeathRequestViewModel> MapToVMList(List<DeathRequest> data)
        {
            return data.Select(req => new DeathRequestViewModel
            {
                Id = req.Id,
                EmployeeName = req.EmployeeName,
                EpfNumber = req.EpfNumber,
                DateOfDeath = req.DateOfDeath,
                Status = req.Status
            }).ToList();
        }
    }
}
