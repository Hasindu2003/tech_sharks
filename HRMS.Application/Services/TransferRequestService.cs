using HRMS.Application.Models;
using HRMS.Domain.Entities.Transfer;
using HRMS.Domain.Entities.Core;
using HRMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using DomainTransfer = HRMS.Domain.Entities.Transfer;

namespace HRMS.Application.Services
{
    public interface ITransferRequestService
    {
        Task<(bool Success, string? Error, int Id)> ApplyTransferAsync(
            string employeeEmail, string employeeName, string epfNumber,
            string currentBranch, string currentDesignation, string department,
            string requestedBranch, string reason, DateTime? preferredDate,
            string requestedBy, string requestedByRole, DateTime joiningDate,
            byte[]? documentData, string? documentFileName, string? documentContentType);

        Task<int> CreateTransferRequestAsync(TransferRequestViewModel request, byte[]? documentData, string? documentFileName, string? documentContentType);
        Task<List<TransferRequestViewModel>> GetAllRequestsAsync();
        Task<List<TransferRequestViewModel>> GetRequestsByUserAsync(string email);
        Task<List<TransferRequestViewModel>> GetPendingRequestsForHRManagerAsync();
        Task<bool> HRManagerReviewAsync(int id, bool approved, string comments);
        Task<List<TransferRequestViewModel>> GetPendingRequestsForBranchManagerAsync(string branch);
        Task<bool> BranchManagerReviewAsync(int id, bool approved, string comments, string reviewerBranch);
        Task<List<TransferRequestViewModel>> GetRequestsForAreaManagerAsync();
        Task<bool> AreaManagerReviewAsync(int id, bool approved, string comments);
        Task<TransferRequestViewModel?> GetRequestByIdAsync(int id);
        Task<(byte[]? Data, string? FileName, string? ContentType)> GetDocumentAsync(int id);
    }

    public class TransferRequestService : ITransferRequestService
    {
        private readonly ApplicationDbContext _context;
        private readonly INotificationService _notificationService;

        private static readonly string[] AllowedDocumentExtensions = { ".pdf", ".doc", ".docx", ".jpg", ".jpeg", ".png" };
        private const long MaxDocumentSizeBytes = 5 * 1024 * 1024;

        public TransferRequestService(ApplicationDbContext context, INotificationService notificationService)
        {
            _context = context;
            _notificationService = notificationService;
        }

        public Task<(bool Success, string? Error, int Id)> ApplyTransferAsync(
            string employeeEmail, string employeeName, string epfNumber,
            string currentBranch, string currentDesignation, string department,
            string requestedBranch, string reason, DateTime? preferredDate,
            string requestedBy, string requestedByRole, DateTime joiningDate,
            byte[]? documentData, string? documentFileName, string? documentContentType)
        {
            if (preferredDate.HasValue)
            {
                var minDate = DateTime.Today.AddDays(7);
                var maxDate = DateTime.Today.AddYears(1);

                if (preferredDate.Value.Date < minDate)
                    return Task.FromResult<(bool, string?, int)>((false, "Preferred date must be at least 7 days from today.", 0));

                if (preferredDate.Value.Date > maxDate)
                    return Task.FromResult<(bool, string?, int)>((false, "Preferred date cannot be more than 1 year from today.", 0));
            }

            if (string.Equals(requestedBranch, currentBranch, StringComparison.OrdinalIgnoreCase))
                return Task.FromResult<(bool, string?, int)>((false, "You cannot request a transfer to your current branch.", 0));

            var yearsOfService = (int)((DateTime.Today - joiningDate).TotalDays / 365.25);

            var vm = new TransferRequestViewModel
            {
                EmployeeName       = employeeName,
                EpfNumber          = epfNumber,
                EmployeeEmail      = employeeEmail,
                CurrentBranch      = currentBranch,
                CurrentDesignation = currentDesignation,
                Department         = department,
                RequestedBranch    = requestedBranch,
                Reason             = reason,
                PreferredDate      = preferredDate,
                YearsOfService     = yearsOfService,
                RequestedBy        = requestedBy,
                RequestedByRole    = requestedByRole
            };

            return CreateTransferRequestAsync(vm, documentData, documentFileName, documentContentType)
                .ContinueWith(t => (true, (string?)null, t.Result));
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

            if (request.RequestedByRole == "HR Manager")
            {
                entity.Status = DomainTransfer.TransferRequestStatus.HRManagerApproved;
                entity.HRManagerReview = "Approved (Initiated by HR)";
                entity.HRManagerReviewDate = DateTime.Now;
                entity.HRManagerComments = "Transfer initiated by HR Manager for administrative purposes.";
            }

            await _context.SaveChangesAsync();

            if (request.RequestedByRole == "HR Manager")
            {
                await _notificationService.CreateNotificationAsync(request.EmployeeEmail,
                    "Administrative Transfer Initiated",
                    $"An administrative transfer has been initiated for you from {request.CurrentBranch} to {request.RequestedBranch}.",
                    CoreNotificationType.Info, entity.Id);
            }

            return entity.Id;
        }

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
            request.Status = approved
                ? DomainTransfer.TransferRequestStatus.HRManagerApproved
                : DomainTransfer.TransferRequestStatus.HRManagerRejected;

            await _context.SaveChangesAsync();

            await _notificationService.CreateNotificationAsync(request.RequestedBy,
                approved ? "Transfer Request Approved by HR Manager" : "Transfer Request Rejected by HR Manager",
                approved
                    ? $"Your transfer request #{request.Id} from {request.CurrentBranch} to {request.RequestedBranch} has been approved by HR Manager."
                    : $"Your transfer request #{request.Id} has been rejected by HR Manager. Comments: {comments}",
                approved ? CoreNotificationType.Approved : CoreNotificationType.Rejected, request.Id);

            return true;
        }

        public async Task<List<TransferRequestViewModel>> GetPendingRequestsForBranchManagerAsync(string branch)
        {
            var entities = await _context.TransferRequests
                .Where(r => (r.Status == DomainTransfer.TransferRequestStatus.HRManagerApproved ||
                             r.Status == DomainTransfer.TransferRequestStatus.CurrentBMApproved ||
                             r.Status == DomainTransfer.TransferRequestStatus.TargetBMApproved) &&
                            (r.CurrentBranch == branch || r.RequestedBranch == branch))
                .OrderByDescending(r => r.RequestedDate)
                .ToListAsync();

            var result = new List<TransferRequestViewModel>();
            foreach (var e in entities)
            {
                bool isCurrentBM = e.CurrentBranch == branch;
                bool isTargetBM = e.RequestedBranch == branch;
                if (isCurrentBM && e.CurrentBMReview != null) continue;
                if (isTargetBM && e.TargetBMReview != null) continue;
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

            if (isCurrentBM && isTargetBM)
            {
                request.CurrentBMReview = approved ? "Approved" : "Rejected"; request.CurrentBMReviewDate = DateTime.Now; request.CurrentBMComments = comments;
                request.TargetBMReview = approved ? "Approved" : "Rejected"; request.TargetBMReviewDate = DateTime.Now; request.TargetBMComments = comments;
            }
            else if (isCurrentBM)
            { request.CurrentBMReview = approved ? "Approved" : "Rejected"; request.CurrentBMReviewDate = DateTime.Now; request.CurrentBMComments = comments; }
            else if (isTargetBM)
            { request.TargetBMReview = approved ? "Approved" : "Rejected"; request.TargetBMReviewDate = DateTime.Now; request.TargetBMComments = comments; }
            else return false;

            if (!approved)
            {
                request.Status = isCurrentBM
                    ? DomainTransfer.TransferRequestStatus.CurrentBMRejected
                    : DomainTransfer.TransferRequestStatus.TargetBMRejected;
            }
            else
            {
                bool currentDone = request.CurrentBMReview == "Approved";
                bool targetDone = request.TargetBMReview == "Approved";
                request.Status = (currentDone && targetDone) ? DomainTransfer.TransferRequestStatus.BothBMsApproved
                    : currentDone ? DomainTransfer.TransferRequestStatus.CurrentBMApproved
                    : DomainTransfer.TransferRequestStatus.TargetBMApproved;
            }

            await _context.SaveChangesAsync();

            var bmLabel = isCurrentBM ? "Current Branch Manager" : "Target Branch Manager";
            await _notificationService.CreateNotificationAsync(request.RequestedBy,
                approved ? $"Transfer Request Approved by {bmLabel}" : $"Transfer Request Rejected by {bmLabel}",
                approved
                    ? $"Your transfer request #{request.Id} has been approved by {bmLabel}."
                    : $"Your transfer request #{request.Id} has been rejected by {bmLabel}. Comments: {comments}",
                approved ? CoreNotificationType.Approved : CoreNotificationType.Rejected, request.Id);

            return true;
        }

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

            await _notificationService.CreateNotificationAsync(request.RequestedBy,
                approved ? "Transfer Request Approved" : "Transfer Request Rejected",
                approved
                    ? $"Your transfer request #{request.Id} from {request.CurrentBranch} to {request.RequestedBranch} has been fully approved."
                    : $"Your transfer request #{request.Id} has been rejected by the Area Manager. Comments: {comments}",
                approved ? CoreNotificationType.Approved : CoreNotificationType.Rejected, request.Id);

            return true;
        }

        public async Task<List<TransferRequestViewModel>> GetAllRequestsAsync()
        {
            var entities = await _context.TransferRequests.OrderByDescending(r => r.RequestedDate).ToListAsync();
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

        private static TransferRequestViewModel MapToViewModel(DomainTransfer.TransferRequest entity) => new()
        {
            Id = entity.Id, EmployeeName = entity.EmployeeName, EpfNumber = entity.EpfNumber,
            EmployeeEmail = entity.EmployeeEmail, CurrentBranch = entity.CurrentBranch,
            CurrentDesignation = entity.CurrentDesignation, Department = entity.Department,
            RequestedBranch = entity.RequestedBranch, Reason = entity.Reason,
            PreferredDate = entity.PreferredDate, YearsOfService = entity.YearsOfService,
            RequestedBy = entity.RequestedBy, RequestedByRole = entity.RequestedByRole,
            RequestedDate = entity.RequestedDate, Status = (TransferStatus)(int)entity.Status,
            DocumentFileName = entity.DocumentFileName, HasDocument = entity.DocumentData != null,
            HRManagerReview = entity.HRManagerReview, HRManagerReviewDate = entity.HRManagerReviewDate,
            HRManagerComments = entity.HRManagerComments, CurrentBMReview = entity.CurrentBMReview,
            CurrentBMReviewDate = entity.CurrentBMReviewDate, CurrentBMComments = entity.CurrentBMComments,
            TargetBMReview = entity.TargetBMReview, TargetBMReviewDate = entity.TargetBMReviewDate,
            TargetBMComments = entity.TargetBMComments, AreaManagerReview = entity.AreaManagerReview,
            AreaManagerReviewDate = entity.AreaManagerReviewDate, AreaManagerComments = entity.AreaManagerComments
        };
    }
}
