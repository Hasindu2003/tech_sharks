using HRMS.Domain.Entities.Termination;
using HRMS.Domain.Entities.Transfer;
using HRMS.Domain.Entities.Core;
using HRMS.Infrastructure.Persistence;
using HRMS.Application.Models;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Application.Services
{
    public interface ITerminationService
    {
        Task<int> CreateTerminationRequestAsync(TerminationRequestViewModel request);
        Task<bool> UpdateTerminationRequestAsync(TerminationRequestViewModel request);
        Task<(bool Success, string? ErrorMessage)> ValidateAndSubmitAsync(int id);
        Task<List<TerminationRequestViewModel>> GetTerminationRequestsAsync(string? statusFilter = null, string? search = null);
        Task<List<TerminationRequestViewModel>> GetPendingApprovalsAsync();
        Task<TerminationRequestViewModel?> GetTerminationByIdAsync(int id);
        Task<bool> ApproveTerminationAsync(int id, string comments, string approverEmail);
        Task<bool> RejectTerminationAsync(int id, string comments, string approverEmail);
        Task<bool> ProcessFinanceClearanceAsync(int id);
        Task<int> AddDocumentAsync(int terminationRequestId, string fileName, string contentType, byte[] data, TerminationDocumentType docType);
        Task<bool> RemoveDocumentAsync(int documentId);
        Task<(byte[]? Data, string? FileName, string? ContentType)> GetDocumentAsync(int documentId);
        Task<List<TerminationDocumentViewModel>> GetDocumentsForRequestAsync(int terminationRequestId);
        Task<List<TerminationRequestViewModel>> GetTerminationsByEmployeeEmailAsync(string employeeEmail);
    }

    /// <summary>
    /// Service responsible for managing the employee termination process.
    /// Handles everything from request initiation, approval, and financial clearance tracking.
    /// </summary>
    public class TerminationService : ITerminationService
    {
        private readonly ApplicationDbContext _context;
        private readonly INotificationService _notificationService;

        public TerminationService(ApplicationDbContext context, INotificationService notificationService)
        {
            _context = context;
            _notificationService = notificationService;
        }

        /// <summary>
        /// Initiates a new termination request.
        /// </summary>
        public async Task<int> CreateTerminationRequestAsync(TerminationRequestViewModel request)
        {
            var entity = new TerminationRequest
            {
                EmployeeName = request.EmployeeName,
                EpfNumber = request.EpfNumber,
                EmployeeEmail = request.EmployeeEmail,
                Branch = request.Branch,
                Department = request.Department,
                Designation = request.Designation,
                TerminationType = (TerminationType)(int)request.TerminationType,
                ReasonForTermination = request.ReasonForTermination,
                InitiationDate = request.InitiationDate,
                EffectiveTerminationDate = request.EffectiveTerminationDate,
                SupervisorRemarks = request.SupervisorRemarks,
                SpecialRemarks = request.SpecialRemarks,
                DirectObligations = request.DirectObligations,
                IndirectObligations = request.IndirectObligations,
                HasOutstandingLoans = request.HasOutstandingLoans,
                IsLoanGuarantor = request.IsLoanGuarantor,
                HasOverridePermission = request.HasOverridePermission,
                Status = TerminationRequestStatus.New,
                InitiatedBy = request.InitiatedBy,
                InitiatedByRole = request.InitiatedByRole,
                CreatedDate = DateTime.Now,
                LastModifiedDate = DateTime.Now
            };

            _context.TerminationRequests.Add(entity);
            await _context.SaveChangesAsync();
            return entity.Id;
        }

        public async Task<bool> UpdateTerminationRequestAsync(TerminationRequestViewModel request)
        {
            var entity = await _context.TerminationRequests.FindAsync(request.Id);
            if (entity == null || entity.Status != TerminationRequestStatus.New)
                return false;

            entity.TerminationType = (TerminationType)(int)request.TerminationType;
            entity.ReasonForTermination = request.ReasonForTermination;
            entity.InitiationDate = request.InitiationDate;
            entity.EffectiveTerminationDate = request.EffectiveTerminationDate;
            entity.SupervisorRemarks = request.SupervisorRemarks;
            entity.SpecialRemarks = request.SpecialRemarks;
            entity.DirectObligations = request.DirectObligations;
            entity.IndirectObligations = request.IndirectObligations;
            entity.HasOutstandingLoans = request.HasOutstandingLoans;
            entity.IsLoanGuarantor = request.IsLoanGuarantor;
            entity.HasOverridePermission = request.HasOverridePermission;
            entity.LastModifiedDate = DateTime.Now;

            await _context.SaveChangesAsync();
            return true;
        }

        /// <summary>
        /// Validates the termination request for mandatory documents and financial obligations 
        /// before submitting it for final approval.
        /// </summary>
        public async Task<(bool Success, string? ErrorMessage)> ValidateAndSubmitAsync(int id)
        {
            var entity = await _context.TerminationRequests
                .Include(t => t.Documents)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (entity == null)
                return (false, "Termination request not found.");

            if (entity.Status != TerminationRequestStatus.New)
                return (false, "Only requests in 'New' status can be submitted.");

            // Validation: mandatory documents
            if (!entity.Documents.Any())
                return (false, "At least one supporting document must be attached before submission.");

            // Validation: financial obligations check
            if (entity.HasOutstandingLoans && !entity.HasOverridePermission)
                return (false, "Employee has outstanding loan balances. The termination cannot proceed until obligations are cleared, or a senior management override is provided.");

            if (entity.IsLoanGuarantor && !entity.HasOverridePermission)
                return (false, "Employee is listed as a loan guarantor for another employee. The termination cannot proceed until the guarantee is released, or a senior management override is provided.");

            // All validations passed — submit
            entity.Status = TerminationRequestStatus.SubmittedForApproval;
            entity.LastModifiedDate = DateTime.Now;
            await _context.SaveChangesAsync();

            return (true, null);
        }

        public async Task<List<TerminationRequestViewModel>> GetTerminationRequestsAsync(string? statusFilter = null, string? search = null)
        {
            var query = _context.TerminationRequests
                .Include(t => t.Documents)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(statusFilter) && Enum.TryParse<TerminationRequestStatus>(statusFilter, out var status))
            {
                query = query.Where(t => t.Status == status);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.ToLower();
                query = query.Where(t =>
                    t.EmployeeName.ToLower().Contains(s) ||
                    t.EpfNumber.ToLower().Contains(s) ||
                    t.EmployeeEmail.ToLower().Contains(s));
            }

            var entities = await query.OrderByDescending(t => t.CreatedDate).ToListAsync();
            return entities.Select(MapToViewModel).ToList();
        }

        public async Task<List<TerminationRequestViewModel>> GetPendingApprovalsAsync()
        {
            var entities = await _context.TerminationRequests
                .Include(t => t.Documents)
                .Where(t => t.Status == TerminationRequestStatus.SubmittedForApproval)
                .OrderByDescending(t => t.CreatedDate)
                .ToListAsync();

            return entities.Select(MapToViewModel).ToList();
        }

        public async Task<TerminationRequestViewModel?> GetTerminationByIdAsync(int id)
        {
            var entity = await _context.TerminationRequests
                .Include(t => t.Documents)
                .FirstOrDefaultAsync(t => t.Id == id);

            return entity == null ? null : MapToViewModel(entity);
        }

        /// <summary>
        /// Approves the termination request and triggers the automatic finance clearance process.
        /// </summary>
        public async Task<bool> ApproveTerminationAsync(int id, string comments, string approverEmail)
        {
            var entity = await _context.TerminationRequests.FindAsync(id);
            if (entity == null || entity.Status != TerminationRequestStatus.SubmittedForApproval)
                return false;

            entity.ApproverReview = "Approved";
            entity.ApproverReviewDate = DateTime.Now;
            entity.ApproverComments = comments;
            entity.ApprovedBy = approverEmail;
            entity.Status = TerminationRequestStatus.Approved;
            entity.LastModifiedDate = DateTime.Now;

            await _context.SaveChangesAsync();

            // Notify initiator
            await _notificationService.CreateNotificationAsync(
                entity.InitiatedBy,
                "Termination Request Approved",
                $"Termination request #{entity.Id} for {entity.EmployeeName} ({entity.EpfNumber}) has been approved. Proceeding to financial clearance stage.",
                CoreNotificationType.Approved,
                entity.Id,
                "/Transfer/Separation?ActiveTab=Termination"
            );

            // Auto-trigger finance clearance
            await ProcessFinanceClearanceAsync(id);

            return true;
        }

        public async Task<bool> RejectTerminationAsync(int id, string comments, string approverEmail)
        {
            var entity = await _context.TerminationRequests.FindAsync(id);
            if (entity == null || entity.Status != TerminationRequestStatus.SubmittedForApproval)
                return false;

            entity.ApproverReview = "Rejected";
            entity.ApproverReviewDate = DateTime.Now;
            entity.ApproverComments = comments;
            entity.ApprovedBy = approverEmail;
            entity.Status = TerminationRequestStatus.Rejected;
            entity.LastModifiedDate = DateTime.Now;

            await _context.SaveChangesAsync();

            // Notify initiator
            await _notificationService.CreateNotificationAsync(
                entity.InitiatedBy,
                "Termination Request Rejected",
                $"Termination request #{entity.Id} for {entity.EmployeeName} ({entity.EpfNumber}) has been rejected. Comments: {comments}",
                CoreNotificationType.Rejected,
                entity.Id,
                "/Transfer/Separation?ActiveTab=Termination"
            );

            return true;
        }

        /// <summary>
        /// Simulates the financial clearance process, including settlement of loans, salary, and deactivation.
        /// </summary>
        public async Task<bool> ProcessFinanceClearanceAsync(int id)
        {
            var entity = await _context.TerminationRequests.FindAsync(id);
            if (entity == null || entity.Status != TerminationRequestStatus.Approved)
                return false;

            // Simulate finance module processing
            entity.Status = TerminationRequestStatus.FinanceClearance;
            entity.LastModifiedDate = DateTime.Now;
            await _context.SaveChangesAsync();

            // Simulate: final salary, leave settlement, loan deductions, etc.
            entity.FinanceClearanceCompleted = true;
            entity.FinanceClearanceDate = DateTime.Now;
            entity.FinanceClearanceNotes = "Financial clearance completed. Final salary calculated, outstanding loans settled, unused leave payments processed, all organizational assets returned.";
            entity.Status = TerminationRequestStatus.Terminated;
            entity.LastModifiedDate = DateTime.Now;

            await _context.SaveChangesAsync();

            // Notify employee
            await _notificationService.CreateNotificationAsync(
                entity.EmployeeEmail,
                "Employment Termination Finalized",
                $"Your employment termination has been finalized effective {entity.EffectiveTerminationDate:MMMM dd, yyyy}. All financial settlements have been processed. Please contact HR for any further information.",
                CoreNotificationType.Info,
                entity.Id,
                "/Transfer/Separation?ActiveTab=Termination"
            );

            // Notify initiator
            await _notificationService.CreateNotificationAsync(
                entity.InitiatedBy,
                "Termination Process Completed",
                $"Termination request #{entity.Id} for {entity.EmployeeName} ({entity.EpfNumber}) has been fully processed. Employee status updated to Terminated.",
                CoreNotificationType.Approved,
                entity.Id,
                "/Transfer/Separation?ActiveTab=Termination"
            );

            return true;
        }

        // -- Document Management --
        public async Task<int> AddDocumentAsync(int terminationRequestId, string fileName, string contentType, byte[] data, TerminationDocumentType docType)
        {
            var doc = new TerminationDocument
            {
                TerminationRequestId = terminationRequestId,
                FileName = fileName,
                ContentType = contentType,
                DocumentData = data,
                DocumentType = docType,
                UploadedDate = DateTime.Now
            };

            _context.TerminationDocuments.Add(doc);
            await _context.SaveChangesAsync();
            return doc.Id;
        }

        public async Task<bool> RemoveDocumentAsync(int documentId)
        {
            var doc = await _context.TerminationDocuments.FindAsync(documentId);
            if (doc == null) return false;

            // Only allow removal if the parent request is still in New status
            var request = await _context.TerminationRequests.FindAsync(doc.TerminationRequestId);
            if (request == null || request.Status != TerminationRequestStatus.New)
                return false;

            _context.TerminationDocuments.Remove(doc);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<(byte[]? Data, string? FileName, string? ContentType)> GetDocumentAsync(int documentId)
        {
            var doc = await _context.TerminationDocuments.FindAsync(documentId);
            return doc == null ? (null, null, null) : (doc.DocumentData, doc.FileName, doc.ContentType);
        }

        public async Task<List<TerminationDocumentViewModel>> GetDocumentsForRequestAsync(int terminationRequestId)
        {
            var docs = await _context.TerminationDocuments
                .Where(d => d.TerminationRequestId == terminationRequestId)
                .OrderByDescending(d => d.UploadedDate)
                .ToListAsync();

            return docs.Select(d => new TerminationDocumentViewModel
            {
                Id = d.Id,
                FileName = d.FileName,
                ContentType = d.ContentType,
                DocumentType = d.DocumentType.ToString(),
                UploadedDate = d.UploadedDate
            }).ToList();
        }

        public async Task<List<TerminationRequestViewModel>> GetTerminationsByEmployeeEmailAsync(string employeeEmail)
        {
            var entities = await _context.TerminationRequests
                .Include(t => t.Documents)
                .Where(t => t.EmployeeEmail == employeeEmail)
                .OrderByDescending(t => t.CreatedDate)
                .ToListAsync();

            return entities.Select(MapToViewModel).ToList();
        }

        // -- Mapper --
        private static TerminationRequestViewModel MapToViewModel(TerminationRequest entity)
        {
            return new TerminationRequestViewModel
            {
                Id = entity.Id,
                EmployeeName = entity.EmployeeName,
                EpfNumber = entity.EpfNumber,
                EmployeeEmail = entity.EmployeeEmail,
                Branch = entity.Branch,
                Department = entity.Department,
                Designation = entity.Designation,
                TerminationType = (TerminationTypeEnum)(int)entity.TerminationType,
                ReasonForTermination = entity.ReasonForTermination,
                InitiationDate = entity.InitiationDate,
                EffectiveTerminationDate = entity.EffectiveTerminationDate,
                SupervisorRemarks = entity.SupervisorRemarks,
                SpecialRemarks = entity.SpecialRemarks,
                DirectObligations = entity.DirectObligations,
                IndirectObligations = entity.IndirectObligations,
                HasOutstandingLoans = entity.HasOutstandingLoans,
                IsLoanGuarantor = entity.IsLoanGuarantor,
                HasOverridePermission = entity.HasOverridePermission,
                Status = (TerminationStatusEnum)(int)entity.Status,
                InitiatedBy = entity.InitiatedBy,
                InitiatedByRole = entity.InitiatedByRole,
                CreatedDate = entity.CreatedDate,
                LastModifiedDate = entity.LastModifiedDate,
                ApproverReview = entity.ApproverReview,
                ApproverReviewDate = entity.ApproverReviewDate,
                ApproverComments = entity.ApproverComments,
                ApprovedBy = entity.ApprovedBy,
                FinanceClearanceCompleted = entity.FinanceClearanceCompleted,
                FinanceClearanceDate = entity.FinanceClearanceDate,
                FinanceClearanceNotes = entity.FinanceClearanceNotes,
                DocumentCount = entity.Documents.Count,
                Documents = entity.Documents.Select(d => new TerminationDocumentViewModel
                {
                    Id = d.Id,
                    FileName = d.FileName,
                    ContentType = d.ContentType,
                    DocumentType = d.DocumentType.ToString(),
                    UploadedDate = d.UploadedDate
                }).ToList()
            };
        }
    }
}
