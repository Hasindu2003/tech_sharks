using HRMS.Domain.Entities.Transfer;
using HRMS.Infrastructure.Persistence;
using HRMS.UI.Models;
using Microsoft.EntityFrameworkCore;
using DomainTransfer = HRMS.Domain.Entities.Transfer;

namespace HRMS.UI.Services
{
    public interface ITransferRequestService
    {
        Task<int> CreateTransferRequestAsync(TransferRequestViewModel request, byte[]? documentData, string? documentFileName, string? documentContentType);
        Task<List<TransferRequestViewModel>> GetAllRequestsAsync();
        Task<List<TransferRequestViewModel>> GetRequestsByUserAsync(string email);

        // HR Manager
        Task<List<TransferRequestViewModel>> GetPendingRequestsForHRManagerAsync();
        Task<bool> HRManagerReviewAsync(int id, bool approved, string comments);

        // Branch Manager (filtered by branch)
        Task<List<TransferRequestViewModel>> GetPendingRequestsForBranchManagerAsync(string branch);
        Task<bool> BranchManagerReviewAsync(int id, bool approved, string comments, string reviewerBranch);

        // Area Manager
        Task<List<TransferRequestViewModel>> GetRequestsForAreaManagerAsync();
        Task<bool> AreaManagerReviewAsync(int id, bool approved, string comments);

        Task<TransferRequestViewModel?> GetRequestByIdAsync(int id);
        Task<(byte[]? Data, string? FileName, string? ContentType)> GetDocumentAsync(int id);
    }

    public class TransferRequestService : ITransferRequestService
    {
        private readonly ApplicationDbContext _context;
        private readonly INotificationService _notificationService;

        public TransferRequestService(ApplicationDbContext context, INotificationService notificationService)
        {
            _context = context;
            _notificationService = notificationService;
        }

        public async Task<int> CreateTransferRequestAsync(TransferRequestViewModel request, byte[]? documentData, string? documentFileName, string? documentContentType)
        {
            var entity = new DomainTransfer.TransferRequest
            {
                EmployeeName = request.EmployeeName,
                EpfNumber = request.EpfNumber,
                EmployeeEmail = request.EmployeeEmail,
                CurrentBranch = request.CurrentBranch,
                CurrentDesignation = request.CurrentDesignation,
                Department = request.Department,
                RequestedBranch = request.RequestedBranch,
                Reason = request.Reason,
                PreferredDate = request.PreferredDate,
                YearsOfService = request.YearsOfService,
                RequestedBy = request.RequestedBy,
                RequestedByRole = request.RequestedByRole,
                RequestedDate = DateTime.Now,
                Status = DomainTransfer.TransferRequestStatus.Pending,
                DocumentData = documentData,
                DocumentFileName = documentFileName,
                DocumentContentType = documentContentType
            };

            _context.TransferRequests.Add(entity);

            // HR Manager requests skip all review stages (HR + BM) → go directly to Area Manager
            if (request.RequestedByRole == "HR Manager")
            {
                entity.Status = DomainTransfer.TransferRequestStatus.BothBMsApproved;
                entity.HRManagerReview = "N/A - Requester is HR Manager";
                entity.HRManagerReviewDate = DateTime.Now;
                entity.CurrentBMReview = "N/A - Requester is HR Manager";
                entity.CurrentBMReviewDate = DateTime.Now;
                entity.TargetBMReview = "N/A - Requester is HR Manager";
                entity.TargetBMReviewDate = DateTime.Now;
            }

            await _context.SaveChangesAsync();
            return entity.Id;
        }

        // ── HR Manager: see all Pending requests ──
        public async Task<List<TransferRequestViewModel>> GetPendingRequestsForHRManagerAsync()
        {
            var entities = await _context.TransferRequests
                .Where(r => r.Status == DomainTransfer.TransferRequestStatus.Pending)
                .OrderByDescending(r => r.RequestedDate)
                .ToListAsync();
            return entities.Select(MapToViewModel).ToList();
        }

        public async Task<bool> HRManagerReviewAsync(int id, bool approved, string comments)
        {
            var request = await _context.TransferRequests.FindAsync(id);
            if (request == null) return false;

            request.HRManagerReview = approved ? "Approved" : "Rejected";
            request.HRManagerReviewDate = DateTime.Now;
            request.HRManagerComments = comments;

            if (!approved)
            {
                request.Status = DomainTransfer.TransferRequestStatus.HRManagerRejected;
            }
            else if (request.RequestedByRole == "Branch Manager")
            {
                // Branch Manager requests skip BM stages → go directly to Area Manager
                request.CurrentBMReview = "N/A - Requester is Branch Manager";
                request.CurrentBMReviewDate = DateTime.Now;
                request.TargetBMReview = "N/A - Requester is Branch Manager";
                request.TargetBMReviewDate = DateTime.Now;
                request.Status = DomainTransfer.TransferRequestStatus.BothBMsApproved;
            }
            else
            {
                request.Status = DomainTransfer.TransferRequestStatus.HRManagerApproved;
            }

            await _context.SaveChangesAsync();

            // Notify the requestor
            await _notificationService.CreateNotificationAsync(
                request.RequestedBy,
                approved ? "Transfer Request Approved by HR Manager" : "Transfer Request Rejected by HR Manager",
                approved
                    ? $"Your transfer request #{request.Id} from {request.CurrentBranch} to {request.RequestedBranch} has been approved by HR Manager and is now moving to the next review stage."
                    : $"Your transfer request #{request.Id} from {request.CurrentBranch} to {request.RequestedBranch} has been rejected by HR Manager. Comments: {comments}",
                approved ? NotificationType.Approved : NotificationType.Rejected,
                request.Id
            );

            return true;
        }

        // ── Branch Manager: see HR-approved requests where current OR target branch matches ──
        public async Task<List<TransferRequestViewModel>> GetPendingRequestsForBranchManagerAsync(string branch)
        {
            var entities = await _context.TransferRequests
                .Where(r => (r.Status == DomainTransfer.TransferRequestStatus.HRManagerApproved ||
                             r.Status == DomainTransfer.TransferRequestStatus.CurrentBMApproved ||
                             r.Status == DomainTransfer.TransferRequestStatus.TargetBMApproved) &&
                            (r.CurrentBranch == branch || r.RequestedBranch == branch))
                .OrderByDescending(r => r.RequestedDate)
                .ToListAsync();

            // Filter out requests that this BM has already reviewed
            var result = new List<TransferRequestViewModel>();
            foreach (var e in entities)
            {
                bool isCurrentBM = e.CurrentBranch == branch;
                bool isTargetBM = e.RequestedBranch == branch;

                // Skip if this BM already reviewed their part
                if (isCurrentBM && e.CurrentBMReview != null) continue;
                if (isTargetBM && e.TargetBMReview != null) continue;
                // If BM is both (same branch transfer), skip if either reviewed
                if (isCurrentBM && isTargetBM && (e.CurrentBMReview != null || e.TargetBMReview != null)) continue;

                result.Add(MapToViewModel(e));
            }

            return result;
        }

        public async Task<bool> BranchManagerReviewAsync(int id, bool approved, string comments, string reviewerBranch)
        {
            var request = await _context.TransferRequests.FindAsync(id);
            if (request == null) return false;

            bool isCurrentBM = request.CurrentBranch == reviewerBranch;
            bool isTargetBM = request.RequestedBranch == reviewerBranch;

            // If same branch (shouldn't happen but handle gracefully), treat as both
            if (isCurrentBM && isTargetBM)
            {
                request.CurrentBMReview = approved ? "Approved" : "Rejected";
                request.CurrentBMReviewDate = DateTime.Now;
                request.CurrentBMComments = comments;
                request.TargetBMReview = approved ? "Approved" : "Rejected";
                request.TargetBMReviewDate = DateTime.Now;
                request.TargetBMComments = comments;
            }
            else if (isCurrentBM)
            {
                request.CurrentBMReview = approved ? "Approved" : "Rejected";
                request.CurrentBMReviewDate = DateTime.Now;
                request.CurrentBMComments = comments;
            }
            else if (isTargetBM)
            {
                request.TargetBMReview = approved ? "Approved" : "Rejected";
                request.TargetBMReviewDate = DateTime.Now;
                request.TargetBMComments = comments;
            }
            else
            {
                return false; // This BM doesn't belong to either branch
            }

            // If rejected by any BM, mark as rejected
            if (!approved)
            {
                request.Status = isCurrentBM
                    ? DomainTransfer.TransferRequestStatus.CurrentBMRejected
                    : DomainTransfer.TransferRequestStatus.TargetBMRejected;
            }
            else
            {
                // Check if both BMs have approved
                bool currentDone = request.CurrentBMReview == "Approved";
                bool targetDone = request.TargetBMReview == "Approved";

                if (currentDone && targetDone)
                {
                    request.Status = DomainTransfer.TransferRequestStatus.BothBMsApproved;
                }
                else if (currentDone)
                {
                    request.Status = DomainTransfer.TransferRequestStatus.CurrentBMApproved;
                }
                else if (targetDone)
                {
                    request.Status = DomainTransfer.TransferRequestStatus.TargetBMApproved;
                }
            }

            await _context.SaveChangesAsync();

            // Notify the requestor
            var bmLabel = isCurrentBM ? "Current Branch Manager" : "Target Branch Manager";
            await _notificationService.CreateNotificationAsync(
                request.RequestedBy,
                approved ? $"Transfer Request Approved by {bmLabel}" : $"Transfer Request Rejected by {bmLabel}",
                approved
                    ? $"Your transfer request #{request.Id} from {request.CurrentBranch} to {request.RequestedBranch} has been approved by {bmLabel}."
                    : $"Your transfer request #{request.Id} from {request.CurrentBranch} to {request.RequestedBranch} has been rejected by {bmLabel}. Comments: {comments}",
                approved ? NotificationType.Approved : NotificationType.Rejected,
                request.Id
            );

            return true;
        }

        // ── Area Manager: see requests where both BMs approved ──
        public async Task<List<TransferRequestViewModel>> GetRequestsForAreaManagerAsync()
        {
            var entities = await _context.TransferRequests
                .Where(r => r.Status == DomainTransfer.TransferRequestStatus.BothBMsApproved)
                .OrderByDescending(r => r.RequestedDate)
                .ToListAsync();
            return entities.Select(MapToViewModel).ToList();
        }

        public async Task<bool> AreaManagerReviewAsync(int id, bool approved, string comments)
        {
            var request = await _context.TransferRequests.FindAsync(id);
            if (request == null) return false;

            request.AreaManagerReview = approved ? "Approved" : "Rejected";
            request.AreaManagerReviewDate = DateTime.Now;
            request.AreaManagerComments = comments;
            request.Status = approved
                ? DomainTransfer.TransferRequestStatus.AreaManagerApproved
                : DomainTransfer.TransferRequestStatus.AreaManagerRejected;

            await _context.SaveChangesAsync();

            // Notify the requestor — final decision
            await _notificationService.CreateNotificationAsync(
                request.RequestedBy,
                approved ? "Transfer Request Approved ✅" : "Transfer Request Rejected ❌",
                approved
                    ? $"Great news! Your transfer request #{request.Id} from {request.CurrentBranch} to {request.RequestedBranch} has been fully approved by the Area Manager. Your transfer will proceed as planned."
                    : $"Your transfer request #{request.Id} from {request.CurrentBranch} to {request.RequestedBranch} has been rejected by the Area Manager. Comments: {comments}",
                approved ? NotificationType.Approved : NotificationType.Rejected,
                request.Id
            );

            return true;
        }

        // ── General queries ──
        public async Task<List<TransferRequestViewModel>> GetAllRequestsAsync()
        {
            var entities = await _context.TransferRequests
                .OrderByDescending(r => r.RequestedDate)
                .ToListAsync();
            return entities.Select(MapToViewModel).ToList();
        }

        public async Task<List<TransferRequestViewModel>> GetRequestsByUserAsync(string email)
        {
            var entities = await _context.TransferRequests
                .Where(r => r.RequestedBy == email)
                .OrderByDescending(r => r.RequestedDate)
                .ToListAsync();
            return entities.Select(MapToViewModel).ToList();
        }

        public async Task<TransferRequestViewModel?> GetRequestByIdAsync(int id)
        {
            var entity = await _context.TransferRequests.FindAsync(id);
            return entity == null ? null : MapToViewModel(entity);
        }

        public async Task<(byte[]? Data, string? FileName, string? ContentType)> GetDocumentAsync(int id)
        {
            var entity = await _context.TransferRequests
                .Where(r => r.Id == id)
                .Select(r => new { r.DocumentData, r.DocumentFileName, r.DocumentContentType })
                .FirstOrDefaultAsync();
            return entity == null ? (null, null, null) : (entity.DocumentData, entity.DocumentFileName, entity.DocumentContentType);
        }

        // ── Mapper ──
        private static TransferRequestViewModel MapToViewModel(DomainTransfer.TransferRequest entity)
        {
            return new TransferRequestViewModel
            {
                Id = entity.Id,
                EmployeeName = entity.EmployeeName,
                EpfNumber = entity.EpfNumber,
                EmployeeEmail = entity.EmployeeEmail,
                CurrentBranch = entity.CurrentBranch,
                CurrentDesignation = entity.CurrentDesignation,
                Department = entity.Department,
                RequestedBranch = entity.RequestedBranch,
                Reason = entity.Reason,
                PreferredDate = entity.PreferredDate,
                YearsOfService = entity.YearsOfService,
                RequestedBy = entity.RequestedBy,
                RequestedByRole = entity.RequestedByRole,
                RequestedDate = entity.RequestedDate,
                Status = (TransferStatus)(int)entity.Status,
                DocumentFileName = entity.DocumentFileName,
                HasDocument = entity.DocumentData != null,
                HRManagerReview = entity.HRManagerReview,
                HRManagerReviewDate = entity.HRManagerReviewDate,
                HRManagerComments = entity.HRManagerComments,
                CurrentBMReview = entity.CurrentBMReview,
                CurrentBMReviewDate = entity.CurrentBMReviewDate,
                CurrentBMComments = entity.CurrentBMComments,
                TargetBMReview = entity.TargetBMReview,
                TargetBMReviewDate = entity.TargetBMReviewDate,
                TargetBMComments = entity.TargetBMComments,
                AreaManagerReview = entity.AreaManagerReview,
                AreaManagerReviewDate = entity.AreaManagerReviewDate,
                AreaManagerComments = entity.AreaManagerComments
            };
        }
    }
}