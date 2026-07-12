using HRMS.Domain.Entities.Resignation;
using HRMS.Domain.Entities.Transfer;
using HRMS.Domain.Entities.Core;
using HRMS.Infrastructure.Persistence;
using HRMS.Application.Models;
using Microsoft.AspNetCore.Identity;
using HRMS.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Application.Services
{
    public interface IResignationService
    {
        Task<int> CreateResignationRequestAsync(ResignationRequestViewModel vm);
        Task<(bool Success, string? Error)> ValidateAndSubmitAsync(int id);
        Task<(bool Success, string? Error)> UpdateDraftAsync(int id, DateTime effectiveDate, string reasonForResignation, string? additionalRemarks, bool hasOutstandingLoans, bool isLoanGuarantor);
        Task<ResignationRequestViewModel?> GetByIdAsync(int id);
        Task<List<ResignationRequestViewModel>> GetMyResignationsAsync(string employeeEmail);
        Task<List<ResignationRequestViewModel>> GetPendingForBranchManagerAsync();
        Task<List<ResignationRequestViewModel>> GetPendingForAreaManagerAsync();
        Task<List<ResignationRequestViewModel>> GetPendingForHRManagerAsync();
        Task<List<ResignationRequestViewModel>> GetAllAsync(string? statusFilter = null, string? search = null);
        Task<bool> BranchManagerApproveAsync(int id, string comments, string reviewerEmail);
        Task<bool> BranchManagerRejectAsync(int id, string comments, string reviewerEmail);
        Task<bool> AreaManagerApproveAsync(int id, string comments, string reviewerEmail);
        Task<bool> AreaManagerRejectAsync(int id, string comments, string reviewerEmail);
        Task<bool> HRManagerApproveAsync(int id, string comments, string reviewerEmail);
        Task<bool> HRManagerRejectAsync(int id, string comments, string reviewerEmail);
        Task<(bool Success, string? Error)> ProcessEffectiveDateAsync(int id, string processedBy, UserManager<ApplicationUser> userManager);
        Task<(bool Success, string? Error)> ReactivateAccountAsync(int id, string reactivatedBy, UserManager<ApplicationUser> userManager);
        Task<int> AddDocumentAsync(int resignationRequestId, string fileName, string contentType, byte[] data);
        Task<(byte[]? Data, string? FileName, string? ContentType)> GetDocumentAsync(int documentId);
    }

    /// <summary>
    /// Service responsible for managing employee resignations.
    /// Handles the end-to-end workflow from submission, manager approvals, to account deactivation.
    /// </summary>
    public class ResignationService : IResignationService
    {
        private readonly ApplicationDbContext _context;
        private readonly INotificationService _notificationService;

        public ResignationService(ApplicationDbContext context, INotificationService notificationService)
        {
            _context = context;
            _notificationService = notificationService;
        }

        // -- Create --
        /// <summary>
        /// Creates a new resignation request in Draft status.
        /// </summary>
        public async Task<int> CreateResignationRequestAsync(ResignationRequestViewModel vm)
        {
            var entity = new ResignationRequest
            {
                EmployeeName       = vm.EmployeeName,
                EpfNumber          = vm.EpfNumber,
                EmployeeEmail      = vm.EmployeeEmail,
                Branch             = vm.Branch,
                Department         = vm.Department,
                Designation        = vm.Designation,
                ReasonForResignation = vm.ReasonForResignation,
                ResignationDate    = vm.ResignationDate,
                EffectiveDate      = vm.EffectiveDate,
                NoticePeriodDays   = vm.NoticePeriodDays,
                AdditionalRemarks  = vm.AdditionalRemarks,
                HasOutstandingLoans = vm.HasOutstandingLoans,
                IsLoanGuarantor    = vm.IsLoanGuarantor,
                HasOverridePermission = vm.HasOverridePermission,
                ObligationDetails  = vm.ObligationDetails,
                Status             = ResignationStatus.Draft,
                InitiatedBy        = vm.InitiatedBy,
                CreatedDate        = DateTime.Now,
                LastModifiedDate   = DateTime.Now
            };

            _context.ResignationRequests.Add(entity);
            await _context.SaveChangesAsync();
            return entity.Id;
        }

        // -- Validate & Submit --
        /// <summary>
        /// Validates that a draft request is ready for submission (e.g., has documents attached)
        /// and moves it to the SubmittedForApproval status.
        /// </summary>
        public async Task<(bool Success, string? Error)> ValidateAndSubmitAsync(int id)
        {
            var entity = await _context.ResignationRequests
                .Include(r => r.Documents)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (entity == null)
                return (false, "Resignation request not found.");

            if (entity.Status != ResignationStatus.Draft)
                return (false, "Only draft requests can be submitted.");

            if (!entity.Documents.Any())
                return (false, "At least one supporting document must be attached before submission.");

            // All validations passed � submit
            entity.Status = ResignationStatus.SubmittedForApproval;
            entity.LastModifiedDate = DateTime.Now;
            await _context.SaveChangesAsync();

            return (true, null);
        }

        // -- Update Draft --
        /// <summary>
        /// Updates an existing Draft resignation with new field values.
        /// Only works if the request is still in Draft status.
        /// </summary>
        public async Task<(bool Success, string? Error)> UpdateDraftAsync(
            int id,
            DateTime effectiveDate,
            string reasonForResignation,
            string? additionalRemarks,
            bool hasOutstandingLoans,
            bool isLoanGuarantor)
        {
            var entity = await _context.ResignationRequests
                .FirstOrDefaultAsync(r => r.Id == id);

            if (entity == null)
                return (false, "Resignation request not found.");

            if (entity.Status != ResignationStatus.Draft)
                return (false, "Only draft requests can be edited.");

            entity.EffectiveDate        = effectiveDate;
            entity.NoticePeriodDays     = (effectiveDate.Date - DateTime.Today).Days;
            entity.ReasonForResignation = reasonForResignation;
            entity.AdditionalRemarks    = additionalRemarks;
            entity.HasOutstandingLoans  = hasOutstandingLoans;
            entity.IsLoanGuarantor      = isLoanGuarantor;
            entity.LastModifiedDate     = DateTime.Now;

            await _context.SaveChangesAsync();
            return (true, null);
        }

        // -- Queries --
        public async Task<ResignationRequestViewModel?> GetByIdAsync(int id)
        {
            var entity = await _context.ResignationRequests
                .Include(r => r.Documents)
                .FirstOrDefaultAsync(r => r.Id == id);
            return entity == null ? null : Map(entity);
        }

        public async Task<List<ResignationRequestViewModel>> GetMyResignationsAsync(string employeeEmail)
        {
            var list = await _context.ResignationRequests
                .Include(r => r.Documents)
                .Where(r => r.EmployeeEmail == employeeEmail)
                .OrderByDescending(r => r.CreatedDate)
                .ToListAsync();
            return list.Select(Map).ToList();
        }

        public async Task<List<ResignationRequestViewModel>> GetPendingForBranchManagerAsync()
        {
            var list = await _context.ResignationRequests
                .Include(r => r.Documents)
                .Where(r => r.Status == ResignationStatus.SubmittedForApproval)
                .OrderByDescending(r => r.CreatedDate)
                .ToListAsync();
            return list.Select(Map).ToList();
        }

        public async Task<List<ResignationRequestViewModel>> GetPendingForAreaManagerAsync()
        {
            var list = await _context.ResignationRequests
                .Include(r => r.Documents)
                .Where(r => r.Status == ResignationStatus.BMApproved)
                .OrderByDescending(r => r.BMReviewDate)
                .ToListAsync();
            return list.Select(Map).ToList();
        }

        public async Task<List<ResignationRequestViewModel>> GetPendingForHRManagerAsync()
        {
            var list = await _context.ResignationRequests
                .Include(r => r.Documents)
                .Where(r => r.Status == ResignationStatus.AMApproved)
                .OrderByDescending(r => r.AMReviewDate)
                .ToListAsync();
            return list.Select(Map).ToList();
        }

        public async Task<List<ResignationRequestViewModel>> GetAllAsync(string? statusFilter = null, string? search = null)
        {
            var query = _context.ResignationRequests
                .Include(r => r.Documents)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(statusFilter) &&
                Enum.TryParse<ResignationStatus>(statusFilter, out var status))
                query = query.Where(r => r.Status == status);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.ToLower();
                query = query.Where(r =>
                    r.EmployeeName.ToLower().Contains(s) ||
                    r.EpfNumber.ToLower().Contains(s) ||
                    r.EmployeeEmail.ToLower().Contains(s));
            }

            var list = await query.OrderByDescending(r => r.CreatedDate).ToListAsync();
            return list.Select(Map).ToList();
        }

        // -- Stage 1: Branch Manager --
        /// <summary>
        /// Approves the resignation at the Branch Manager level.
        /// </summary>
        public async Task<bool> BranchManagerApproveAsync(int id, string comments, string reviewerEmail)
        {
            var entity = await _context.ResignationRequests.FindAsync(id);
            if (entity == null || entity.Status != ResignationStatus.SubmittedForApproval)
                return false;

            entity.BMReview     = "Approved";
            entity.BMReviewDate = DateTime.Now;
            entity.BMComments   = comments;
            entity.BMEmail      = reviewerEmail;
            entity.Status       = ResignationStatus.BMApproved;
            entity.LastModifiedDate = DateTime.Now;

            await _context.SaveChangesAsync();

            await _notificationService.CreateNotificationAsync(
                entity.EmployeeEmail,
                "Resignation Acknowledged by Branch Manager",
                $"Your resignation request has been acknowledged by your Branch Manager. It is now pending Area Manager review. Effective date: {entity.EffectiveDate:MMMM dd, yyyy}.",
                CoreNotificationType.Approved,
                entity.Id,
                "/Transfer/Separation?ActiveTab=Resignation");

            return true;
        }

        public async Task<bool> BranchManagerRejectAsync(int id, string comments, string reviewerEmail)
        {
            var entity = await _context.ResignationRequests.FindAsync(id);
            if (entity == null || entity.Status != ResignationStatus.SubmittedForApproval)
                return false;

            entity.BMReview     = "Rejected";
            entity.BMReviewDate = DateTime.Now;
            entity.BMComments   = comments;
            entity.BMEmail      = reviewerEmail;
            entity.Status       = ResignationStatus.BMRejected;
            entity.LastModifiedDate = DateTime.Now;

            await _context.SaveChangesAsync();

            await _notificationService.CreateNotificationAsync(
                entity.EmployeeEmail,
                "Resignation Request Rejected",
                $"Your resignation request has been rejected by your Branch Manager. Reason: {comments}",
                CoreNotificationType.Rejected,
                entity.Id,
                "/Transfer/Separation?ActiveTab=Resignation");

            return true;
        }

        // -- Stage 2: Area Manager --
        /// <summary>
        /// Approves the resignation at the Area Manager level.
        /// </summary>
        public async Task<bool> AreaManagerApproveAsync(int id, string comments, string reviewerEmail)
        {
            var entity = await _context.ResignationRequests.FindAsync(id);
            if (entity == null || entity.Status != ResignationStatus.BMApproved)
                return false;

            entity.AMReview     = "Approved";
            entity.AMReviewDate = DateTime.Now;
            entity.AMComments   = comments;
            entity.AMEmail      = reviewerEmail;
            entity.Status       = ResignationStatus.AMApproved;
            entity.LastModifiedDate = DateTime.Now;

            await _context.SaveChangesAsync();

            await _notificationService.CreateNotificationAsync(
                entity.EmployeeEmail,
                "Resignation Approved by Area Manager",
                $"Your resignation request has been approved by the Area Manager. It is now pending final HR Manager approval. Effective date: {entity.EffectiveDate:MMMM dd, yyyy}.",
                CoreNotificationType.Approved,
                entity.Id,
                "/Transfer/Separation?ActiveTab=Resignation");

            return true;
        }

        public async Task<bool> AreaManagerRejectAsync(int id, string comments, string reviewerEmail)
        {
            var entity = await _context.ResignationRequests.FindAsync(id);
            if (entity == null || entity.Status != ResignationStatus.BMApproved)
                return false;

            entity.AMReview     = "Rejected";
            entity.AMReviewDate = DateTime.Now;
            entity.AMComments   = comments;
            entity.AMEmail      = reviewerEmail;
            entity.Status       = ResignationStatus.AMRejected;
            entity.LastModifiedDate = DateTime.Now;

            await _context.SaveChangesAsync();

            await _notificationService.CreateNotificationAsync(
                entity.EmployeeEmail,
                "Resignation Request Rejected",
                $"Your resignation request has been rejected by the Area Manager. Reason: {comments}",
                CoreNotificationType.Rejected,
                entity.Id,
                "/Transfer/Separation?ActiveTab=Resignation");

            return true;
        }

        // -- Stage 3: HR Manager --
        /// <summary>
        /// Approves the resignation at the HR Manager level (Final approval).
        /// Generates the acceptance letter and prepares for account deactivation.
        /// </summary>
        public async Task<bool> HRManagerApproveAsync(int id, string comments, string reviewerEmail)
        {
            var entity = await _context.ResignationRequests.FindAsync(id);
            if (entity == null || entity.Status != ResignationStatus.AMApproved)
                return false;

            entity.HRReview     = "Approved";
            entity.HRReviewDate = DateTime.Now;
            entity.HRComments   = comments;
            entity.HREmail      = reviewerEmail;
            entity.Status       = ResignationStatus.HRApproved;
            entity.AcceptanceLetterGenerated = true;
            entity.AcceptanceLetterDate      = DateTime.Now;
            entity.LastModifiedDate          = DateTime.Now;

            await _context.SaveChangesAsync();

            // Notify employee � acceptance letter ready
            await _notificationService.CreateNotificationAsync(
                entity.EmployeeEmail,
                "Resignation Accepted � Letter Available",
                $"Your resignation has been officially approved by HR. Your acceptance letter is now available. Your last working day is {entity.EffectiveDate:MMMM dd, yyyy}. Please ensure all handover tasks are completed.",
                CoreNotificationType.Approved,
                entity.Id,
                "/Transfer/Separation?ActiveTab=Resignation");

            // Notify HR themselves as a reminder to process on effective date
            await _notificationService.CreateNotificationAsync(
                reviewerEmail,
                $"Reminder: Process Account Deactivation on {entity.EffectiveDate:dd MMM yyyy}",
                $"Resignation of {entity.EmployeeName} ({entity.EpfNumber}) has been approved. Please remember to process account deactivation on the effective date: {entity.EffectiveDate:MMMM dd, yyyy}. Use the 'Process Effective Date' action on the resignation details page.",
                CoreNotificationType.Info,
                entity.Id,
                "/HRManager/ReviewResignations");

            return true;
        }

        public async Task<bool> HRManagerRejectAsync(int id, string comments, string reviewerEmail)
        {
            var entity = await _context.ResignationRequests.FindAsync(id);
            if (entity == null || entity.Status != ResignationStatus.AMApproved)
                return false;

            entity.HRReview     = "Rejected";
            entity.HRReviewDate = DateTime.Now;
            entity.HRComments   = comments;
            entity.HREmail      = reviewerEmail;
            entity.Status       = ResignationStatus.HRRejected;
            entity.LastModifiedDate = DateTime.Now;

            await _context.SaveChangesAsync();

            await _notificationService.CreateNotificationAsync(
                entity.EmployeeEmail,
                "Resignation Request Rejected by HR",
                $"Your resignation request has been rejected by the HR Manager. Reason: {comments}. Please contact HR for further clarification.",
                CoreNotificationType.Rejected,
                entity.Id,
                "/Transfer/Separation?ActiveTab=Resignation");

            return true;
        }

        // -- Process Effective Date (account deactivation) --
        public async Task<(bool Success, string? Error)> ProcessEffectiveDateAsync(
            int id, string processedBy, UserManager<ApplicationUser> userManager)
        {
            var entity = await _context.ResignationRequests.FindAsync(id);
            if (entity == null)
                return (false, "Request not found.");

            if (entity.Status != ResignationStatus.HRApproved)
                return (false, "Only HR-approved resignations can be processed.");

            if (entity.AccountDeactivated)
                return (false, "Account has already been deactivated.");

            if (DateTime.Today < entity.EffectiveDate.Date)
                return (false, $"Effective date is {entity.EffectiveDate:MMMM dd, yyyy}. Cannot process before that date.");

            // Deactivate: lock the account without deleting it
            var user = await userManager.FindByEmailAsync(entity.EmployeeEmail);
            if (user == null)
                return (false, "Employee account not found.");

            // Lock out the account permanently (until re-activated)
            user.LockoutEnabled = true;
            user.LockoutEnd = DateTimeOffset.MaxValue;
            await userManager.UpdateAsync(user);

            entity.AccountDeactivated    = true;
            entity.AccountDeactivatedDate = DateTime.Now;
            entity.AccountDeactivatedBy  = processedBy;
            entity.Status                = ResignationStatus.Completed;
            entity.LastModifiedDate      = DateTime.Now;

            await _context.SaveChangesAsync();

            // Notify employee
            await _notificationService.CreateNotificationAsync(
                entity.EmployeeEmail,
                "Employment Ended � Account Deactivated",
                $"Your employment has officially ended on {entity.EffectiveDate:MMMM dd, yyyy}. Your account has been deactivated. Please contact HR if you need any assistance.",
                CoreNotificationType.Info,
                entity.Id,
                "/Transfer/Separation?ActiveTab=Resignation");

            return (true, null);
        }

        // -- Reactivate Account --
        public async Task<(bool Success, string? Error)> ReactivateAccountAsync(
            int id, string reactivatedBy, UserManager<ApplicationUser> userManager)
        {
            var entity = await _context.ResignationRequests.FindAsync(id);
            if (entity == null)
                return (false, "Request not found.");

            if (!entity.AccountDeactivated)
                return (false, "Account is not deactivated.");

            var user = await userManager.FindByEmailAsync(entity.EmployeeEmail);
            if (user == null)
                return (false, "Employee account not found.");

            // Remove lockout
            user.LockoutEnd = null;
            user.LockoutEnabled = false;
            await userManager.UpdateAsync(user);

            entity.AccountDeactivated    = false;
            entity.AccountDeactivatedDate = null;
            entity.AccountDeactivatedBy  = null;
            entity.LastModifiedDate      = DateTime.Now;

            await _context.SaveChangesAsync();

            // Notify employee
            await _notificationService.CreateNotificationAsync(
                entity.EmployeeEmail,
                "Account Reactivated",
                $"Your account has been reactivated by {reactivatedBy}. You can now log in to the HRMS portal.",
                CoreNotificationType.Approved,
                entity.Id,
                "/Transfer/Separation?ActiveTab=Resignation");

            return (true, null);
        }

        // -- Documents --
        public async Task<int> AddDocumentAsync(int resignationRequestId, string fileName, string contentType, byte[] data)
        {
            var doc = new ResignationDocument
            {
                ResignationRequestId = resignationRequestId,
                FileName    = fileName,
                ContentType = contentType,
                DocumentData = data,
                UploadedDate = DateTime.Now
            };
            _context.ResignationDocuments.Add(doc);
            await _context.SaveChangesAsync();
            return doc.Id;
        }

        public async Task<(byte[]? Data, string? FileName, string? ContentType)> GetDocumentAsync(int documentId)
        {
            var doc = await _context.ResignationDocuments.FindAsync(documentId);
            return doc == null ? (null, null, null) : (doc.DocumentData, doc.FileName, doc.ContentType);
        }

        // -- Mapper --
        private static ResignationRequestViewModel Map(ResignationRequest e) => new()
        {
            Id                        = e.Id,
            EmployeeName              = e.EmployeeName,
            EpfNumber                 = e.EpfNumber,
            EmployeeEmail             = e.EmployeeEmail,
            Branch                    = e.Branch,
            Department                = e.Department,
            Designation               = e.Designation,
            ReasonForResignation      = e.ReasonForResignation,
            ResignationDate           = e.ResignationDate,
            EffectiveDate             = e.EffectiveDate,
            NoticePeriodDays          = e.NoticePeriodDays,
            AdditionalRemarks         = e.AdditionalRemarks,
            HasOutstandingLoans       = e.HasOutstandingLoans,
            IsLoanGuarantor           = e.IsLoanGuarantor,
            HasOverridePermission     = e.HasOverridePermission,
            ObligationDetails         = e.ObligationDetails,
            Status                    = (ResignationStatusEnum)(int)e.Status,
            InitiatedBy               = e.InitiatedBy,
            CreatedDate               = e.CreatedDate,
            LastModifiedDate          = e.LastModifiedDate,
            BMReview                  = e.BMReview,
            BMReviewDate              = e.BMReviewDate,
            BMComments                = e.BMComments,
            BMEmail                   = e.BMEmail,
            AMReview                  = e.AMReview,
            AMReviewDate              = e.AMReviewDate,
            AMComments                = e.AMComments,
            AMEmail                   = e.AMEmail,
            HRReview                  = e.HRReview,
            HRReviewDate              = e.HRReviewDate,
            HRComments                = e.HRComments,
            HREmail                   = e.HREmail,
            AcceptanceLetterGenerated = e.AcceptanceLetterGenerated,
            AcceptanceLetterDate      = e.AcceptanceLetterDate,
            AccountDeactivated        = e.AccountDeactivated,
            AccountDeactivatedDate    = e.AccountDeactivatedDate,
            AccountDeactivatedBy      = e.AccountDeactivatedBy,
            DocumentCount             = e.Documents.Count,
            Documents = e.Documents.Select(d => new ResignationDocumentViewModel
            {
                Id          = d.Id,
                FileName    = d.FileName,
                ContentType = d.ContentType,
                UploadedDate = d.UploadedDate
            }).ToList()
        };
    }
}
