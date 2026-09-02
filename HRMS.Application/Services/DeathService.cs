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
        Task<List<DeathRequestViewModel>> GetAllPendingForAMAsync(List<int>? branchIds = null, string? branchName = null);
        Task<List<DeathRequestViewModel>> GetAllPendingForHRAsync();

        Task<List<DeathRequestViewModel>> GetReviewedForBMAsync(string branch);
        Task<List<DeathRequestViewModel>> GetReviewedForAMAsync(List<int>? branchIds = null, string? branchName = null);
        Task<List<DeathRequestViewModel>> GetReviewedForHRAsync();
        
        Task<int> SubmitRequestAsync(DeathRequestViewModel model, List<IFormFile> documents, string initiatedByEmail);

        Task<bool> BMApproveAsync(int id, string comments, string bmEmail);
        Task<bool> BMRejectAsync(int id, string comments, string bmEmail);

        Task<bool> AMApproveAsync(int id, string comments, string amEmail);
        Task<bool> AMRejectAsync(int id, string comments, string amEmail);

        Task<bool> HRManagerApproveAsync(int id, string comments, string hrEmail, UserManager<ApplicationUser>? userManager = null);
        Task<bool> HRManagerRejectAsync(int id, string comments, string hrEmail);

        Task<(byte[] Content, string ContentType, string FileName)?> DownloadDocumentAsync(int documentId);
        
        Task<(bool Success, string ErrorMessage)> ProcessClosureAsync(int id, string hrEmail, UserManager<ApplicationUser> userManager);
    }

    /// <summary>
    /// Service responsible for managing the process following an employee's death.
    /// Handles BM initiation -> AM review & confirmation -> HR Manager finalization & closure.
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
            var branchLower = (branch ?? "").Trim().ToLower();
            var data = await _context.DeathRequests
                .Where(r => r.Status == DeathRequestStatus.SubmittedForApproval && 
                            r.Branch.Trim().ToLower() == branchLower)
                .OrderByDescending(r => r.CreatedDate)
                .ToListAsync();
            return MapToVMList(data);
        }

        public async Task<List<DeathRequestViewModel>> GetAllPendingForAMAsync(List<int>? branchIds = null, string? branchName = null)
        {
            var query = _context.DeathRequests
                .Where(r => r.Status == DeathRequestStatus.BMApproved || r.Status == DeathRequestStatus.SubmittedForApproval);

            if (branchIds != null && branchIds.Any())
            {
                var branchNames = await _context.Branches
                    .Where(b => branchIds.Contains(b.Id))
                    .Select(b => b.Name.ToLower())
                    .ToListAsync();

                query = query.Where(r => branchNames.Contains(r.Branch.ToLower()));
            }
            else if (!string.IsNullOrWhiteSpace(branchName))
            {
                var bn = branchName.Trim().ToLower();
                query = query.Where(r => r.Branch.ToLower() == bn);
            }

            var data = await query.OrderByDescending(r => r.CreatedDate).ToListAsync();
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

        public async Task<List<DeathRequestViewModel>> GetReviewedForBMAsync(string branch)
        {
            var branchLower = (branch ?? "").Trim().ToLower();
            var data = await _context.DeathRequests
                .Where(r => r.Branch.Trim().ToLower() == branchLower)
                .OrderByDescending(r => r.LastModifiedDate)
                .ToListAsync();
            return MapToVMList(data);
        }

        public async Task<List<DeathRequestViewModel>> GetReviewedForAMAsync(List<int>? branchIds = null, string? branchName = null)
        {
            var query = _context.DeathRequests
                .Where(r => r.AMReviewDate.HasValue || r.Status == DeathRequestStatus.AMApproved || r.Status == DeathRequestStatus.AMRejected || r.Status == DeathRequestStatus.Completed || r.Status == DeathRequestStatus.HRApproved || r.Status == DeathRequestStatus.HRRejected);

            if (branchIds != null && branchIds.Any())
            {
                var branchNames = await _context.Branches
                    .Where(b => branchIds.Contains(b.Id))
                    .Select(b => b.Name.ToLower())
                    .ToListAsync();

                query = query.Where(r => branchNames.Contains(r.Branch.ToLower()));
            }
            else if (!string.IsNullOrWhiteSpace(branchName))
            {
                var bn = branchName.Trim().ToLower();
                query = query.Where(r => r.Branch.ToLower() == bn);
            }

            var data = await query.OrderByDescending(r => r.LastModifiedDate).ToListAsync();
            return MapToVMList(data);
        }

        public async Task<List<DeathRequestViewModel>> GetReviewedForHRAsync()
        {
            var data = await _context.DeathRequests
                .Where(r => r.HRReviewDate.HasValue || r.Status == DeathRequestStatus.Completed || r.Status == DeathRequestStatus.HRRejected)
                .OrderByDescending(r => r.LastModifiedDate)
                .ToListAsync();
            return MapToVMList(data);
        }

        /// <summary>
        /// Submits a new death request with supporting documentation.
        /// Initiated by Branch Manager and forwards directly to Area Manager review.
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
                Status = DeathRequestStatus.BMApproved, // Auto-marked as initiated/approved by BM -> moves to Area Manager review
                InitiatedBy = initiatedByEmail,
                BMReview = "Approved",
                BMReviewDate = DateTime.UtcNow,
                BMComments = string.IsNullOrWhiteSpace(model.AdditionalRemarks) ? "Initiated by Branch Manager" : model.AdditionalRemarks,
                BMEmail = initiatedByEmail,
                CreatedDate = DateTime.UtcNow,
                LastModifiedDate = DateTime.UtcNow
            };

            if (documents != null)
            {
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
            }

            _context.DeathRequests.Add(entity);
            await _context.SaveChangesAsync();

            // Notify all Area Managers
            await NotifyRoleUsersAsync(
                "Area Manager",
                "New Employee Death Process Initiated",
                $"A death process for {entity.EmployeeName} (EPF: {entity.EpfNumber}, Branch: {entity.Branch}) has been initiated and requires Area Manager review.",
                CoreNotificationType.Info,
                $"/AreaManager/ReviewDeath/{entity.Id}"
            );

            return entity.Id;
        }

        public async Task<bool> BMApproveAsync(int id, string comments, string bmEmail)
        {
            var req = await _context.DeathRequests.FindAsync(id);
            if (req == null) return false;

            req.Status = DeathRequestStatus.BMApproved;
            req.BMReview = "Approved";
            req.BMReviewDate = DateTime.UtcNow;
            req.BMComments = comments;
            req.BMEmail = bmEmail;
            req.LastModifiedDate = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            await NotifyRoleUsersAsync(
                "Area Manager",
                "Death Request Pending Review",
                $"Death request for {req.EmployeeName} (EPF: {req.EpfNumber}, Branch: {req.Branch}) is awaiting Area Manager review.",
                CoreNotificationType.Info,
                $"/AreaManager/ReviewDeath/{req.Id}"
            );

            return true;
        }

        public async Task<bool> BMRejectAsync(int id, string comments, string bmEmail)
        {
            var req = await _context.DeathRequests.FindAsync(id);
            if (req == null) return false;

            req.Status = DeathRequestStatus.BMRejected;
            req.BMReview = "Rejected";
            req.BMReviewDate = DateTime.UtcNow;
            req.BMComments = comments;
            req.BMEmail = bmEmail;
            req.LastModifiedDate = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            await NotifyUserByEmailAsync(
                req.InitiatedBy,
                "Death Request Rejected",
                $"The death request for {req.EmployeeName} was rejected by Branch Manager. Reason: {comments}",
                CoreNotificationType.Rejected,
                "/Separation/Dashboard?ActiveTab=Death"
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

            // Notify HR Managers and HR Officers
            await NotifyRoleUsersAsync(
                "HR Manager",
                "Death Request Pending HR Finalization",
                $"Death process for {req.EmployeeName} (EPF: {req.EpfNumber}, Branch: {req.Branch}) was confirmed by Area Manager and is awaiting HR finalization.",
                CoreNotificationType.Info,
                $"/HRManager/ReviewDeath/{req.Id}"
            );

            await NotifyRoleUsersAsync(
                "HR Officer",
                "Death Request Pending HR Finalization",
                $"Death process for {req.EmployeeName} (EPF: {req.EpfNumber}, Branch: {req.Branch}) was confirmed by Area Manager and is awaiting HR finalization.",
                CoreNotificationType.Info,
                $"/HRManager/ReviewDeath/{req.Id}"
            );

            // Notify Branch Manager (Initiator)
            await NotifyUserByEmailAsync(
                req.InitiatedBy,
                "Death Process Confirmed by Area Manager",
                $"Death process for {req.EmployeeName} was confirmed by Area Manager and forwarded to HR Manager for finalization.",
                CoreNotificationType.Info,
                "/Separation/Dashboard?ActiveTab=Death"
            );

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

            // Notify Branch Manager
            await NotifyUserByEmailAsync(
                req.InitiatedBy,
                "Death Request Rejected by Area Manager",
                $"Death request for {req.EmployeeName} was rejected by Area Manager. Reason: {comments}",
                CoreNotificationType.Rejected,
                "/Separation/Dashboard?ActiveTab=Death"
            );

            return true;
        }

        /// <summary>
        /// Processes final HR Manager review and automatically finalizes system closure.
        /// </summary>
        public async Task<bool> HRManagerApproveAsync(int id, string comments, string hrEmail, UserManager<ApplicationUser>? userManager = null)
        {
            var req = await _context.DeathRequests.FindAsync(id);
            if (req == null || (req.Status != DeathRequestStatus.AMApproved && req.Status != DeathRequestStatus.HRApproved)) return false;

            req.Status = DeathRequestStatus.Completed;
            req.HRReview = "Approved";
            req.HRReviewDate = DateTime.UtcNow;
            req.HRComments = comments;
            req.HREmail = hrEmail;
            req.LastModifiedDate = DateTime.UtcNow;

            // System closure
            req.AccountDeactivated = true;
            req.AccountDeactivatedDate = DateTime.UtcNow;
            req.AccountDeactivatedBy = hrEmail;
            req.PayrollStopped = true;
            req.FinanceClearanceTriggered = true;

            // Update Employee Record in database
            var emp = await _context.Employees.FirstOrDefaultAsync(e => 
                (!string.IsNullOrEmpty(req.EmployeeEmail) && e.Email == req.EmployeeEmail) || 
                (!string.IsNullOrEmpty(req.EpfNumber) && e.EPFNumber == req.EpfNumber));

            if (emp != null)
            {
                emp.Status = "Deceased";
            }

            // Lockout user credentials in Identity
            if (userManager != null && !string.IsNullOrEmpty(req.EmployeeEmail))
            {
                var user = await userManager.FindByEmailAsync(req.EmployeeEmail);
                if (user != null)
                {
                    user.LockoutEnabled = true;
                    user.LockoutEnd = DateTimeOffset.MaxValue;
                    await userManager.UpdateAsync(user);
                }
            }

            await _context.SaveChangesAsync();

            // Notify Branch Manager (Initiator)
            await NotifyUserByEmailAsync(
                req.InitiatedBy,
                "Employee Death Process Finalized & Completed",
                $"The death process for {req.EmployeeName} (EPF: {req.EpfNumber}) has been finalized and closed by HR.",
                CoreNotificationType.Approved,
                "/Separation/Dashboard?ActiveTab=Death"
            );

            // Notify Area Manager
            if (!string.IsNullOrEmpty(req.AMEmail))
            {
                await NotifyUserByEmailAsync(
                    req.AMEmail,
                    "Employee Death Process Finalized & Completed",
                    $"The death process for {req.EmployeeName} ({req.Branch}) has been finalized and closed by HR.",
                    CoreNotificationType.Approved,
                    $"/AreaManager/ReviewDeath/{req.Id}"
                );
            }

            return true;
        }

        public async Task<bool> HRManagerRejectAsync(int id, string comments, string hrEmail)
        {
            var req = await _context.DeathRequests.FindAsync(id);
            if (req == null || (req.Status != DeathRequestStatus.AMApproved && req.Status != DeathRequestStatus.HRApproved)) return false;

            req.Status = DeathRequestStatus.HRRejected;
            req.HRReview = "Rejected";
            req.HRReviewDate = DateTime.UtcNow;
            req.HRComments = comments;
            req.HREmail = hrEmail;
            req.LastModifiedDate = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            // Notify BM & AM
            await NotifyUserByEmailAsync(
                req.InitiatedBy,
                "Death Process Rejected by HR",
                $"The death process for {req.EmployeeName} was rejected by HR Manager. Remarks: {comments}",
                CoreNotificationType.Rejected,
                "/Separation/Dashboard?ActiveTab=Death"
            );

            if (!string.IsNullOrEmpty(req.AMEmail))
            {
                await NotifyUserByEmailAsync(
                    req.AMEmail,
                    "Death Process Rejected by HR",
                    $"The death process for {req.EmployeeName} ({req.Branch}) was rejected by HR Manager. Remarks: {comments}",
                    CoreNotificationType.Rejected,
                    $"/AreaManager/ReviewDeath/{req.Id}"
                );
            }

            return true;
        }

        private async Task NotifyRoleUsersAsync(string roleName, string title, string message, CoreNotificationType type, string targetUrl)
        {
            try
            {
                var role = await _context.Roles.FirstOrDefaultAsync(r => r.Name == roleName);
                if (role == null) return;

                var userIdsInRole = await _context.UserRoles
                    .Where(ur => ur.RoleId == role.Id)
                    .Select(ur => ur.UserId)
                    .ToListAsync();

                var users = await _context.Users
                    .Where(u => userIdsInRole.Contains(u.Id))
                    .ToListAsync();

                foreach (var user in users)
                {
                    if (!string.IsNullOrWhiteSpace(user.Email))
                    {
                        await _notificationService.CreateNotificationAsync(user.Email, title, message, type, targetUrl);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DeathService Notification Error]: {ex.Message}");
            }
        }

        private async Task NotifyUserByEmailAsync(string email, string title, string message, CoreNotificationType type, string targetUrl)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(email)) return;
                await _notificationService.CreateNotificationAsync(email, title, message, type, targetUrl);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DeathService Notification Error]: {ex.Message}");
            }
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
            var success = await HRManagerApproveAsync(id, "Final system closure processed", hrEmail, userManager);
            if (!success)
                return (false, "Unable to complete closure for this request.");

            return (true, string.Empty);
        }

        private List<DeathRequestViewModel> MapToVMList(List<DeathRequest> data)
        {
            return data.Select(req => new DeathRequestViewModel
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
                Status = req.Status,
                InitiatedBy = req.InitiatedBy,
                CreatedDate = req.CreatedDate,
                LastModifiedDate = req.LastModifiedDate,
                BMReview = req.BMReview,
                BMReviewDate = req.BMReviewDate,
                AMReview = req.AMReview,
                AMReviewDate = req.AMReviewDate,
                HRReview = req.HRReview,
                HRReviewDate = req.HRReviewDate
            }).ToList();
        }
    }
}
