using HRMS.Domain.Entities.Transfer;
using HRMS.Infrastructure.Persistence;
using HRMS.Application.Models;
using Microsoft.EntityFrameworkCore;
using DomainTransfer = HRMS.Domain.Entities.Transfer;

namespace HRMS.Application.Services
{
    public interface ITransferRequestService
    {
        Task<int> CreateTransferRequestAsync(TransferRequestViewModel request, byte[]? documentData, string? documentFileName, string? documentContentType);
        Task<List<TransferRequestViewModel>> GetAllRequestsAsync();
        Task<List<TransferRequestViewModel>> GetRequestsByUserAsync(string email);

        // Stage 2 – Department Head
        Task<List<TransferRequestViewModel>> GetRequestsForDeptHeadAsync(string branch, string department);
        Task<List<TransferRequestViewModel>> GetReviewedByDeptHeadAsync(string branch, string department);
        Task<bool> DeptHeadReviewAsync(int id, bool approved, string comments);

        // Stage 3 – Branch Managers (parallel)
        Task<List<TransferRequestViewModel>> GetPendingRequestsForBranchManagerAsync(string branch);
        Task<List<TransferRequestViewModel>> GetReviewedByBranchManagerAsync(string branch);
        Task<bool> BranchManagerReviewAsync(int id, bool approved, string comments, string reviewerBranch);

        // Stage 4 – Area Manager
        Task<List<TransferRequestViewModel>> GetRequestsForAreaManagerAsync();
        Task<List<TransferRequestViewModel>> GetReviewedByAreaManagerAsync();
        Task<bool> AreaManagerReviewAsync(int id, bool approved, string comments);

        // Stage 5 – HR Finalization
        Task<List<TransferRequestViewModel>> GetRequestsForHRManagerAsync(string branch);
        Task<List<TransferRequestViewModel>> GetRequestsForHRFinalizationAsync();
        Task<bool> HRManagerReviewAsync(int id, bool approved, string comments);

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

        // ── Stage 1: Create ──────────────────────────────────────────────────
        public async Task<int> CreateTransferRequestAsync(TransferRequestViewModel request, byte[]? documentData, string? documentFileName, string? documentContentType)
        {
            var entity = new DomainTransfer.TransferRequest
            {
                EmployeeName     = request.EmployeeName,
                EpfNumber        = request.EpfNumber,
                EmployeeEmail    = request.EmployeeEmail,
                CurrentBranch    = request.CurrentBranch,
                CurrentDesignation = request.CurrentDesignation,
                Department       = request.Department,
                RequestedBranch  = request.RequestedBranch,
                Reason           = request.Reason,
                PreferredDate    = request.PreferredDate,
                YearsOfService   = request.YearsOfService,
                JoinDate         = request.JoinDate,
                RequestedBy      = request.RequestedBy,
                RequestedByRole  = request.RequestedByRole,
                RequestedDate    = DateTime.Now,
                Status           = DomainTransfer.TransferRequestStatus.Pending,
                DocumentData     = documentData,
                DocumentFileName = documentFileName,
                DocumentContentType = documentContentType
            };

            _context.TransferRequests.Add(entity);
            await _context.SaveChangesAsync();
            return entity.Id;
        }

        // ── Stage 2: Department Head ─────────────────────────────────────────
        public async Task<List<TransferRequestViewModel>> GetRequestsForDeptHeadAsync(string branch, string department)
        {
            var entities = await _context.TransferRequests
                .Where(r => r.Status == DomainTransfer.TransferRequestStatus.Pending
                         && r.CurrentBranch == branch
                         && r.Department == department)
                .OrderByDescending(r => r.RequestedDate)
                .ToListAsync();
            return entities.Select(MapToViewModel).ToList();
        }

        public async Task<List<TransferRequestViewModel>> GetReviewedByDeptHeadAsync(string branch, string department)
        {
            var entities = await _context.TransferRequests
                .Where(r => r.DeptHeadReview != null
                         && r.CurrentBranch == branch
                         && r.Department == department)
                .OrderByDescending(r => r.DeptHeadReviewDate)
                .ToListAsync();
            return entities.Select(MapToViewModel).ToList();
        }

        public async Task<bool> DeptHeadReviewAsync(int id, bool approved, string comments)
        {
            var request = await _context.TransferRequests.FindAsync(id);
            if (request == null || request.Status != DomainTransfer.TransferRequestStatus.Pending) return false;

            request.DeptHeadReview     = approved ? "Approved" : "Rejected";
            request.DeptHeadReviewDate = DateTime.Now;
            request.DeptHeadComments   = comments;
            request.Status = approved
                ? DomainTransfer.TransferRequestStatus.DeptHeadApproved
                : DomainTransfer.TransferRequestStatus.DeptHeadRejected;

            await _context.SaveChangesAsync();

            await _notificationService.CreateNotificationAsync(
                request.RequestedBy,
                approved ? "Transfer Approved by Department Head" : "Transfer Rejected by Department Head ❌",
                approved
                    ? $"Transfer request #{request.Id} for {request.EmployeeName} has been approved by the Department Head and is now awaiting Branch Manager reviews."
                    : $"Transfer request #{request.Id} for {request.EmployeeName} has been rejected by the Department Head. Comments: {comments}",
                approved ? NotificationType.Approved : NotificationType.Rejected,
                request.Id,
                $"/Transfer/Details/{request.Id}"
            );

            return true;
        }

        // ── Stage 3: Branch Managers (parallel) ──────────────────────────────
        public async Task<List<TransferRequestViewModel>> GetPendingRequestsForBranchManagerAsync(string branch)
        {
            var entities = await _context.TransferRequests
                .Where(r => (r.Status == DomainTransfer.TransferRequestStatus.DeptHeadApproved ||
                             r.Status == DomainTransfer.TransferRequestStatus.CurrentBMApproved  ||
                             r.Status == DomainTransfer.TransferRequestStatus.TargetBMApproved)
                         && (r.CurrentBranch == branch || r.RequestedBranch == branch))
                .OrderByDescending(r => r.RequestedDate)
                .ToListAsync();

            var result = new List<TransferRequestViewModel>();
            foreach (var e in entities)
            {
                bool isCurrentBM = e.CurrentBranch == branch;
                bool isTargetBM  = e.RequestedBranch == branch;

                if (isCurrentBM && e.CurrentBMReview != null) continue;
                if (isTargetBM  && e.TargetBMReview  != null) continue;
                if (isCurrentBM && isTargetBM && (e.CurrentBMReview != null || e.TargetBMReview != null)) continue;

                result.Add(MapToViewModel(e));
            }
            return result;
        }

        public async Task<List<TransferRequestViewModel>> GetReviewedByBranchManagerAsync(string branch)
        {
            var entities = await _context.TransferRequests
                .Where(r => (r.CurrentBranch == branch && r.CurrentBMReview != null)
                         || (r.RequestedBranch == branch && r.TargetBMReview != null))
                .OrderByDescending(r => r.CurrentBMReviewDate ?? r.TargetBMReviewDate)
                .ToListAsync();
            return entities.Select(MapToViewModel).ToList();
        }

        public async Task<bool> BranchManagerReviewAsync(int id, bool approved, string comments, string reviewerBranch)
        {
            var request = await _context.TransferRequests.FindAsync(id);
            if (request == null) return false;

            if (request.Status != DomainTransfer.TransferRequestStatus.DeptHeadApproved &&
                request.Status != DomainTransfer.TransferRequestStatus.CurrentBMApproved &&
                request.Status != DomainTransfer.TransferRequestStatus.TargetBMApproved)
                return false;

            bool isCurrentBM = request.CurrentBranch  == reviewerBranch;
            bool isTargetBM  = request.RequestedBranch == reviewerBranch;

            if (!isCurrentBM && !isTargetBM) return false;

            if (isCurrentBM)
            {
                request.CurrentBMReview     = approved ? "Approved" : "Rejected";
                request.CurrentBMReviewDate = DateTime.Now;
                request.CurrentBMComments   = comments;
            }
            if (isTargetBM)
            {
                request.TargetBMReview     = approved ? "Approved" : "Rejected";
                request.TargetBMReviewDate = DateTime.Now;
                request.TargetBMComments   = comments;
            }

            if (!approved)
            {
                request.Status = isCurrentBM
                    ? DomainTransfer.TransferRequestStatus.CurrentBMRejected
                    : DomainTransfer.TransferRequestStatus.TargetBMRejected;
            }
            else
            {
                bool currentDone = request.CurrentBMReview == "Approved";
                bool targetDone  = request.TargetBMReview  == "Approved";

                if (currentDone && targetDone)
                    request.Status = DomainTransfer.TransferRequestStatus.BothBMsApproved;
                else if (currentDone)
                    request.Status = DomainTransfer.TransferRequestStatus.CurrentBMApproved;
                else
                    request.Status = DomainTransfer.TransferRequestStatus.TargetBMApproved;
            }

            await _context.SaveChangesAsync();

            var bmLabel = isCurrentBM ? "Current Branch Manager" : "Target Branch Manager";
            await _notificationService.CreateNotificationAsync(
                request.RequestedBy,
                approved ? $"Transfer Approved by {bmLabel}" : $"Transfer Rejected by {bmLabel} ❌",
                approved
                    ? $"Transfer request #{request.Id} has been approved by {bmLabel}."
                    : $"Transfer request #{request.Id} has been rejected by {bmLabel}. Comments: {comments}",
                approved ? NotificationType.Approved : NotificationType.Rejected,
                request.Id,
                $"/Transfer/Details/{request.Id}"
            );

            return true;
        }

        // ── Stage 4: Area Manager ─────────────────────────────────────────────
        public async Task<List<TransferRequestViewModel>> GetReviewedByAreaManagerAsync()
        {
            var entities = await _context.TransferRequests
                .Where(r => r.AreaManagerReview != null)
                .OrderByDescending(r => r.AreaManagerReviewDate)
                .ToListAsync();
            return entities.Select(MapToViewModel).ToList();
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
            if (request == null || request.Status != DomainTransfer.TransferRequestStatus.BothBMsApproved) return false;

            request.AreaManagerReview     = approved ? "Approved" : "Rejected";
            request.AreaManagerReviewDate = DateTime.Now;
            request.AreaManagerComments   = comments;
            request.Status = approved
                ? DomainTransfer.TransferRequestStatus.AreaManagerApproved
                : DomainTransfer.TransferRequestStatus.AreaManagerRejected;

            await _context.SaveChangesAsync();

            await _notificationService.CreateNotificationAsync(
                request.RequestedBy,
                approved ? "Transfer Approved by Area Manager – Awaiting HR Finalization" : "Transfer Rejected by Area Manager ❌",
                approved
                    ? $"Transfer request #{request.Id} has been approved by the Area Manager and is now awaiting final HR approval."
                    : $"Transfer request #{request.Id} has been rejected by the Area Manager. Comments: {comments}",
                approved ? NotificationType.Approved : NotificationType.Rejected,
                request.Id,
                $"/Transfer/Details/{request.Id}"
            );

            return true;
        }

        // ── Stage 5: HR Finalization ──────────────────────────────────────────
        public async Task<List<TransferRequestViewModel>> GetRequestsForHRManagerAsync(string branch)
        {
            var entities = await _context.TransferRequests
                .Where(r => r.CurrentBranch == branch || r.RequestedBranch == branch)
                .OrderByDescending(r => r.RequestedDate)
                .ToListAsync();
            return entities.Select(MapToViewModel).ToList();
        }

        public async Task<List<TransferRequestViewModel>> GetRequestsForHRFinalizationAsync()
        {
            var entities = await _context.TransferRequests
                .Where(r => r.Status == DomainTransfer.TransferRequestStatus.AreaManagerApproved)
                .OrderByDescending(r => r.RequestedDate)
                .ToListAsync();
            return entities.Select(MapToViewModel).ToList();
        }

        public async Task<bool> HRManagerReviewAsync(int id, bool approved, string comments)
        {
            var request = await _context.TransferRequests.FindAsync(id);
            if (request == null || request.Status != DomainTransfer.TransferRequestStatus.AreaManagerApproved) return false;

            request.HRManagerReview     = approved ? "Approved" : "Rejected";
            request.HRManagerReviewDate = DateTime.Now;
            request.HRManagerComments   = comments;
            request.Status = approved
                ? DomainTransfer.TransferRequestStatus.FullyApproved
                : DomainTransfer.TransferRequestStatus.HRFinalRejected;

            await _context.SaveChangesAsync();

            await _notificationService.CreateNotificationAsync(
                request.RequestedBy,
                approved ? "Transfer Fully Approved ✅" : "Transfer Rejected at HR Finalization ❌",
                approved
                    ? $"Transfer request #{request.Id} for {request.EmployeeName} from {request.CurrentBranch} to {request.RequestedBranch} has been fully approved and finalized by HR."
                    : $"Transfer request #{request.Id} has been rejected at HR finalization. Comments: {comments}",
                approved ? NotificationType.Approved : NotificationType.Rejected,
                request.Id,
                $"/Transfer/Details/{request.Id}"
            );

            if (approved)
            {
                await _notificationService.CreateNotificationAsync(
                    request.EmployeeEmail,
                    "Your Transfer Has Been Approved ✅",
                    $"Your transfer from {request.CurrentBranch} to {request.RequestedBranch} has been fully approved. Please coordinate with your managers for the transition.",
                    NotificationType.Approved,
                    request.Id,
                    $"/Transfer/Details/{request.Id}"
                );
            }

            return true;
        }

        // ── General queries ───────────────────────────────────────────────────
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
                .Where(r => r.RequestedBy == email || r.EmployeeEmail == email)
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

        // ── Mapper ────────────────────────────────────────────────────────────
        private static TransferRequestViewModel MapToViewModel(DomainTransfer.TransferRequest entity)
        {
            return new TransferRequestViewModel
            {
                Id                 = entity.Id,
                EmployeeName       = entity.EmployeeName,
                EpfNumber          = entity.EpfNumber,
                EmployeeEmail      = entity.EmployeeEmail,
                CurrentBranch      = entity.CurrentBranch,
                CurrentDesignation = entity.CurrentDesignation,
                Department         = entity.Department,
                RequestedBranch    = entity.RequestedBranch,
                Reason             = entity.Reason,
                PreferredDate      = entity.PreferredDate,
                YearsOfService     = entity.YearsOfService,
                JoinDate           = entity.JoinDate,
                RequestedBy        = entity.RequestedBy,
                RequestedByRole    = entity.RequestedByRole,
                RequestedDate      = entity.RequestedDate,
                Status             = (TransferStatus)(int)entity.Status,
                DocumentFileName   = entity.DocumentFileName,
                HasDocument        = entity.DocumentData != null,
                DeptHeadReview     = entity.DeptHeadReview,
                DeptHeadReviewDate = entity.DeptHeadReviewDate,
                DeptHeadComments   = entity.DeptHeadComments,
                CurrentBMReview     = entity.CurrentBMReview,
                CurrentBMReviewDate = entity.CurrentBMReviewDate,
                CurrentBMComments   = entity.CurrentBMComments,
                TargetBMReview     = entity.TargetBMReview,
                TargetBMReviewDate = entity.TargetBMReviewDate,
                TargetBMComments   = entity.TargetBMComments,
                AreaManagerReview     = entity.AreaManagerReview,
                AreaManagerReviewDate = entity.AreaManagerReviewDate,
                AreaManagerComments   = entity.AreaManagerComments,
                HRManagerReview     = entity.HRManagerReview,
                HRManagerReviewDate = entity.HRManagerReviewDate,
                HRManagerComments   = entity.HRManagerComments,
            };
        }
    }
}
