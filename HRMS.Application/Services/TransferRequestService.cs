using HRMS.Domain.Entities.Transfer;
using HRMS.Domain.Entities.Core;
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
        Task<(string Branch, string Department)> ResolveDepartmentHeadScopeAsync(string userId);
        Task<List<TransferRequestViewModel>> GetRequestsForDeptHeadAsync(string branch, string department);
        Task<List<TransferRequestViewModel>> GetReviewedByDeptHeadAsync(string branch, string department);
        Task<bool> DeptHeadReviewAsync(int id, bool approved, string comments);
        bool IsDeptHeadResponsibleFor(TransferRequestViewModel request, string branch, string department);

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
        Task<bool> HRManagerMarkAsReviewedAsync(int id, string comments, string reviewerEmail);

        Task<TransferRequestViewModel?> GetRequestByIdAsync(int id);
        Task<(byte[]? Data, string? FileName, string? ContentType)> GetDocumentAsync(int id);
        Task<bool> IsManagerialRequestAsync(DomainTransfer.TransferRequest request);
        Task<bool> IsManagerialEmployeeAsync(string? email, string? epf, string? designation, string? requestedByRole, string? department = null);
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

            bool isManager = await IsManagerialEmployeeAsync(request.EmployeeEmail, request.EpfNumber, request.CurrentDesignation, null, request.Department);
            if (isManager)
            {
                entity.Status = DomainTransfer.TransferRequestStatus.PendingHRReview;
                _context.TransferRequests.Add(entity);
                await _context.SaveChangesAsync();

                // 1. Notify HR Managers directly
                var hrRecipients = await GetHRManagerUserIdentifiersAsync();
                await SendNotificationsAsync(
                    hrRecipients,
                    "Managerial Transfer Request Submitted (For Review)",
                    $"Transfer request #{entity.Id} submitted by {entity.RequestedByRole} ({entity.EmployeeName}, {entity.CurrentBranch} → {entity.RequestedBranch}) has been submitted for HR review.",
                    CoreNotificationType.Info,
                    entity.Id,
                    $"/HRManager/ReviewTransfer/{entity.Id}"
                );

                // 2. Notify Manager / Employee
                var empUserIds = await GetEmployeeUserIdentifiersAsync(entity.EmployeeEmail, entity.EpfNumber, entity.RequestedBy);
                await SendNotificationsAsync(
                    empUserIds,
                    "Transfer Request Submitted",
                    $"Your transfer request #{entity.Id} to {entity.RequestedBranch} has been submitted directly to the HR Manager for review.",
                    CoreNotificationType.Info,
                    entity.Id,
                    $"/Transfer/Details/{entity.Id}"
                );

                return entity.Id;
            }

            _context.TransferRequests.Add(entity);
            await _context.SaveChangesAsync();

            // 1. Notify Department Head(s) responsible for this branch + department
            var deptHeadIds = await GetDepartmentHeadUserIdentifiersAsync(entity.CurrentBranch, entity.Department);
            await SendNotificationsAsync(
                deptHeadIds,
                "New Transfer Request Pending Review",
                $"Transfer request #{entity.Id} for {entity.EmployeeName} ({entity.CurrentBranch} - {entity.Department}) is pending your approval.",
                CoreNotificationType.Info,
                entity.Id,
                $"/DepartmentHead/ReviewTransfer/{entity.Id}"
            );

            // 2. Notify HR Officer(s) managing Current / Target branch
            var hrOfficerIds = await GetHROfficerUserIdentifiersAsync(entity.CurrentBranch, entity.RequestedBranch);
            await SendNotificationsAsync(
                hrOfficerIds,
                "Transfer Request Initiated",
                $"Transfer request #{entity.Id} for {entity.EmployeeName} ({entity.CurrentBranch} → {entity.RequestedBranch}) has been initiated by {entity.RequestedByRole}.",
                CoreNotificationType.Info,
                entity.Id,
                $"/Transfer/Details/{entity.Id}"
            );

            // 3. Notify Employee (if request was initiated on their behalf)
            var empUserIdsRegular = await GetEmployeeUserIdentifiersAsync(entity.EmployeeEmail, entity.EpfNumber, entity.RequestedBy);
            await SendNotificationsAsync(
                empUserIdsRegular,
                "Transfer Request Submitted",
                $"Your transfer request #{entity.Id} to {entity.RequestedBranch} has been submitted and is pending Department Head review.",
                CoreNotificationType.Info,
                entity.Id,
                $"/Transfer/Details/{entity.Id}"
            );

            return entity.Id;
        }

        // ── Stage 2: Department Head Review ──────────────────────────────────
        public async Task<(string Branch, string Department)> ResolveDepartmentHeadScopeAsync(string userId)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null) return (string.Empty, string.Empty);

            string branch = user.Branch ?? string.Empty;
            string dept = user.Department ?? string.Empty;

            if (user.EmployeeId.HasValue)
            {
                var emp = await _context.Employees
                    .Include(e => e.Branch)
                    .Include(e => e.Department)
                    .FirstOrDefaultAsync(e => e.Id == user.EmployeeId.Value);

                if (emp != null)
                {
                    if (string.IsNullOrWhiteSpace(branch) && emp.Branch != null)
                        branch = emp.Branch.Name;
                    if (string.IsNullOrWhiteSpace(dept) && emp.Department != null)
                        dept = emp.Department.Name;
                }
            }

            return (branch, dept);
        }

        public bool IsDeptHeadResponsibleFor(TransferRequestViewModel request, string branch, string department)
        {
            if (string.IsNullOrWhiteSpace(branch) || string.IsNullOrWhiteSpace(department)) return false;
            return string.Equals(request.CurrentBranch?.Trim(), branch.Trim(), StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(request.Department?.Trim(), department.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        public async Task<List<TransferRequestViewModel>> GetRequestsForDeptHeadAsync(string branch, string department)
        {
            if (string.IsNullOrWhiteSpace(branch) || string.IsNullOrWhiteSpace(department))
                return new List<TransferRequestViewModel>();

            var branchKey = branch.Trim();
            var departmentKey = department.Trim();

            var entities = await _context.TransferRequests
                .Where(r => (r.Status == DomainTransfer.TransferRequestStatus.Pending || r.Status == DomainTransfer.TransferRequestStatus.PendingHRReview)
                         && r.CurrentBranch != null
                         && r.Department != null)
                .OrderByDescending(r => r.RequestedDate)
                .ToListAsync();

            var matching = entities.Where(r =>
                !string.IsNullOrWhiteSpace(r.CurrentBranch) &&
                !string.IsNullOrWhiteSpace(r.Department) &&
                string.Equals(r.CurrentBranch.Trim(), branchKey, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(r.Department.Trim(), departmentKey, StringComparison.OrdinalIgnoreCase)
            ).ToList();

            var result = new List<TransferRequestViewModel>();
            bool anyUpdated = false;
            foreach (var req in matching)
            {
                if (await IsManagerialRequestAsync(req))
                {
                    if (req.Status == DomainTransfer.TransferRequestStatus.Pending)
                    {
                        req.Status = DomainTransfer.TransferRequestStatus.PendingHRReview;
                        anyUpdated = true;
                    }
                    continue; // Exclude managerial requests from Dept Head
                }
                else
                {
                    // If it was mistakenly marked as PendingHRReview but employee is non-managerial, restore to Pending
                    if (req.Status == DomainTransfer.TransferRequestStatus.PendingHRReview)
                    {
                        req.Status = DomainTransfer.TransferRequestStatus.Pending;
                        anyUpdated = true;
                    }
                }
                result.Add(MapToViewModel(req));
            }
            if (anyUpdated)
            {
                await _context.SaveChangesAsync();
            }

            return result;
        }

        public async Task<List<TransferRequestViewModel>> GetReviewedByDeptHeadAsync(string branch, string department)
        {
            if (string.IsNullOrWhiteSpace(branch) || string.IsNullOrWhiteSpace(department))
                return new List<TransferRequestViewModel>();

            var branchKey = branch.Trim();
            var departmentKey = department.Trim();

            var entities = await _context.TransferRequests
                .Where(r => r.DeptHeadReview != null
                         && r.CurrentBranch != null
                         && r.Department != null)
                .OrderByDescending(r => r.DeptHeadReviewDate)
                .ToListAsync();

            var matching = entities.Where(r =>
                !string.IsNullOrWhiteSpace(r.CurrentBranch) &&
                !string.IsNullOrWhiteSpace(r.Department) &&
                string.Equals(r.CurrentBranch.Trim(), branchKey, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(r.Department.Trim(), departmentKey, StringComparison.OrdinalIgnoreCase)
            ).ToList();

            var result = new List<TransferRequestViewModel>();
            foreach (var req in matching)
            {
                if (await IsManagerialRequestAsync(req)) continue;
                result.Add(MapToViewModel(req));
            }
            return result;
        }

        public async Task<bool> DeptHeadReviewAsync(int id, bool approved, string comments)
        {
            var request = await _context.TransferRequests.FindAsync(id);
            if (request == null || request.Status != DomainTransfer.TransferRequestStatus.Pending) return false;
            if (await IsManagerialRequestAsync(request)) return false;

            request.DeptHeadReview     = approved ? "Approved" : "Rejected";
            request.DeptHeadReviewDate = DateTime.Now;
            request.DeptHeadComments   = comments;
            request.Status = approved
                ? DomainTransfer.TransferRequestStatus.DeptHeadApproved
                : DomainTransfer.TransferRequestStatus.DeptHeadRejected;

            await _context.SaveChangesAsync();

            if (approved)
            {
                // 1. Notify Current Branch Manager
                var currentBMs = await GetBranchManagerUserIdentifiersAsync(request.CurrentBranch);
                await SendNotificationsAsync(
                    currentBMs,
                    "Transfer Request Pending Review",
                    $"Transfer request #{request.Id} for {request.EmployeeName} (outgoing from {request.CurrentBranch}) has been approved by the Department Head and awaits your review.",
                    CoreNotificationType.Info,
                    request.Id,
                    $"/BranchManager/ReviewTransfer/{request.Id}"
                );

                // 2. Notify Target Branch Manager
                var targetBMs = await GetBranchManagerUserIdentifiersAsync(request.RequestedBranch);
                await SendNotificationsAsync(
                    targetBMs,
                    "Incoming Transfer Request Pending Review",
                    $"Incoming transfer request #{request.Id} for {request.EmployeeName} (transferring to {request.RequestedBranch}) has been approved by the Department Head and awaits your review.",
                    CoreNotificationType.Info,
                    request.Id,
                    $"/BranchManager/ReviewTransfer/{request.Id}"
                );

                // 3. Notify HR Officers
                var hrOfficers = await GetHROfficerUserIdentifiersAsync(request.CurrentBranch, request.RequestedBranch);
                await SendNotificationsAsync(
                    hrOfficers,
                    "Transfer Approved by Department Head ✅",
                    $"Transfer request #{request.Id} for {request.EmployeeName} was approved by the Department Head and is now awaiting Branch Manager reviews.",
                    CoreNotificationType.Approved,
                    request.Id,
                    $"/Transfer/Details/{request.Id}"
                );

                // 4. Notify Employee & Initiator
                var empAndInitiator = await GetEmployeeUserIdentifiersAsync(request.EmployeeEmail, request.EpfNumber, request.RequestedBy);
                await SendNotificationsAsync(
                    empAndInitiator,
                    "Transfer Approved by Department Head ✅",
                    $"Transfer request #{request.Id} for {request.EmployeeName} has been approved by the Department Head and is now awaiting Branch Manager reviews.",
                    CoreNotificationType.Approved,
                    request.Id,
                    $"/Transfer/Details/{request.Id}"
                );
            }
            else
            {
                // Rejection notifications
                var hrOfficers = await GetHROfficerUserIdentifiersAsync(request.CurrentBranch, request.RequestedBranch);
                await SendNotificationsAsync(
                    hrOfficers,
                    "Transfer Rejected by Department Head ❌",
                    $"Transfer request #{request.Id} for {request.EmployeeName} was rejected by the Department Head. Reason: {comments}",
                    CoreNotificationType.Rejected,
                    request.Id,
                    $"/Transfer/Details/{request.Id}"
                );

                var empAndInitiator = await GetEmployeeUserIdentifiersAsync(request.EmployeeEmail, request.EpfNumber, request.RequestedBy);
                await SendNotificationsAsync(
                    empAndInitiator,
                    "Transfer Rejected by Department Head ❌",
                    $"Transfer request #{request.Id} for {request.EmployeeName} has been rejected by the Department Head. Reason: {comments}",
                    CoreNotificationType.Rejected,
                    request.Id,
                    $"/Transfer/Details/{request.Id}"
                );
            }

            return true;
        }

        // ── Stage 3: Branch Managers (parallel) ──────────────────────────────
        public async Task<List<TransferRequestViewModel>> GetPendingRequestsForBranchManagerAsync(string branch)
        {
            if (string.IsNullOrWhiteSpace(branch)) return new List<TransferRequestViewModel>();

            var branchKey = branch.Trim().ToLower();

            var entities = await _context.TransferRequests
                .Where(r => (r.Status == DomainTransfer.TransferRequestStatus.DeptHeadApproved ||
                             r.Status == DomainTransfer.TransferRequestStatus.CurrentBMApproved  ||
                             r.Status == DomainTransfer.TransferRequestStatus.TargetBMApproved)
                         && ((r.CurrentBranch != null && r.CurrentBranch.Trim().ToLower() == branchKey) ||
                             (r.RequestedBranch != null && r.RequestedBranch.Trim().ToLower() == branchKey)))
                .OrderByDescending(r => r.RequestedDate)
                .ToListAsync();

            var result = new List<TransferRequestViewModel>();
            bool anyUpdated = false;
            foreach (var e in entities)
            {
                if (await IsManagerialRequestAsync(e))
                {
                    if (e.Status != DomainTransfer.TransferRequestStatus.PendingHRReview && e.Status != DomainTransfer.TransferRequestStatus.ManagerReviewed)
                    {
                        e.Status = DomainTransfer.TransferRequestStatus.PendingHRReview;
                        anyUpdated = true;
                    }
                    continue; // Exclude managerial requests from Branch Manager
                }

                bool isCurrentBM = BranchMatches(e.CurrentBranch, branch);
                bool isTargetBM  = BranchMatches(e.RequestedBranch, branch);

                if (isCurrentBM && e.CurrentBMReview != null) continue;
                if (isTargetBM  && e.TargetBMReview  != null) continue;
                if (isCurrentBM && isTargetBM && (e.CurrentBMReview != null || e.TargetBMReview != null)) continue;

                result.Add(MapToViewModel(e));
            }
            if (anyUpdated)
            {
                await _context.SaveChangesAsync();
            }
            return result;
        }

        public async Task<List<TransferRequestViewModel>> GetReviewedByBranchManagerAsync(string branch)
        {
            if (string.IsNullOrWhiteSpace(branch)) return new List<TransferRequestViewModel>();

            var branchKey = branch.Trim().ToLower();

            var entities = await _context.TransferRequests
                .Where(r => (r.CurrentBranch != null && r.CurrentBranch.Trim().ToLower() == branchKey && r.CurrentBMReview != null)
                         || (r.RequestedBranch != null && r.RequestedBranch.Trim().ToLower() == branchKey && r.TargetBMReview != null))
                .OrderByDescending(r => r.CurrentBMReviewDate ?? r.TargetBMReviewDate)
                .ToListAsync();

            var result = new List<TransferRequestViewModel>();
            foreach (var e in entities)
            {
                if (await IsManagerialRequestAsync(e)) continue;
                result.Add(MapToViewModel(e));
            }
            return result;
        }

        private static bool BranchMatches(string? candidate, string? branch)
            => !string.IsNullOrWhiteSpace(candidate)
               && !string.IsNullOrWhiteSpace(branch)
               && string.Equals(candidate.Trim(), branch.Trim(), StringComparison.OrdinalIgnoreCase);

        public async Task<bool> BranchManagerReviewAsync(int id, bool approved, string comments, string reviewerBranch)
        {
            var request = await _context.TransferRequests.FindAsync(id);
            if (request == null) return false;
            if (await IsManagerialRequestAsync(request)) return false;

            if (request.Status != DomainTransfer.TransferRequestStatus.DeptHeadApproved &&
                request.Status != DomainTransfer.TransferRequestStatus.CurrentBMApproved &&
                request.Status != DomainTransfer.TransferRequestStatus.TargetBMApproved)
                return false;

            bool isCurrentBM = BranchMatches(request.CurrentBranch, reviewerBranch);
            bool isTargetBM  = BranchMatches(request.RequestedBranch, reviewerBranch);

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

            if (request.Status == DomainTransfer.TransferRequestStatus.BothBMsApproved)
            {
                // Both BMs Approved -> Escalate to Area Manager
                var areaManagers = await GetAreaManagerUserIdentifiersAsync(request.CurrentBranch, request.RequestedBranch);
                await SendNotificationsAsync(
                    areaManagers,
                    "Transfer Request Awaiting Area Manager Approval",
                    $"Transfer request #{request.Id} for {request.EmployeeName} ({request.CurrentBranch} → {request.RequestedBranch}) has been approved by both Branch Managers and awaits your approval.",
                    CoreNotificationType.Info,
                    request.Id,
                    $"/AreaManager/ReviewTransfer/{request.Id}"
                );

                // Department Head
                var deptHeads = await GetDepartmentHeadUserIdentifiersAsync(request.CurrentBranch, request.Department);
                await SendNotificationsAsync(
                    deptHeads,
                    "Transfer Approved by Both Branch Managers ✅",
                    $"Transfer request #{request.Id} for {request.EmployeeName} has received all Branch Manager approvals and forwarded to Area Manager.",
                    CoreNotificationType.Approved,
                    request.Id,
                    $"/DepartmentHead/ReviewTransfer/{request.Id}"
                );

                // Branch Managers
                var allBMs = (await GetBranchManagerUserIdentifiersAsync(request.CurrentBranch))
                    .Concat(await GetBranchManagerUserIdentifiersAsync(request.RequestedBranch));
                await SendNotificationsAsync(
                    allBMs,
                    "Transfer Approved by Both Branch Managers ✅",
                    $"Transfer request #{request.Id} for {request.EmployeeName} has received all Branch Manager approvals and is now with the Area Manager.",
                    CoreNotificationType.Approved,
                    request.Id,
                    $"/BranchManager/ReviewTransfer/{request.Id}"
                );

                // HR Officers
                var hrOfficers = await GetHROfficerUserIdentifiersAsync(request.CurrentBranch, request.RequestedBranch);
                await SendNotificationsAsync(
                    hrOfficers,
                    "Transfer Approved by Branch Managers ✅",
                    $"Transfer request #{request.Id} for {request.EmployeeName} has been approved by both Branch Managers and escalated to Area Manager.",
                    CoreNotificationType.Approved,
                    request.Id,
                    $"/Transfer/Details/{request.Id}"
                );

                // Employee & Initiator
                var empAndInitiator = await GetEmployeeUserIdentifiersAsync(request.EmployeeEmail, request.EpfNumber, request.RequestedBy);
                await SendNotificationsAsync(
                    empAndInitiator,
                    "Transfer Approved by Branch Managers ✅",
                    $"Transfer request #{request.Id} for {request.EmployeeName} has been approved by both Branch Managers and forwarded to Area Manager.",
                    CoreNotificationType.Approved,
                    request.Id,
                    $"/Transfer/Details/{request.Id}"
                );
            }
            else if (approved)
            {
                // Only one BM approved so far -> Notify the pending other BM
                var otherBranch = isCurrentBM ? request.RequestedBranch : request.CurrentBranch;
                var pendingBMs = await GetBranchManagerUserIdentifiersAsync(otherBranch);
                await SendNotificationsAsync(
                    pendingBMs,
                    "Branch Transfer Pending Your Review",
                    $"Transfer request #{request.Id} for {request.EmployeeName} has been approved by the {bmLabel}. Your review is now pending.",
                    CoreNotificationType.Info,
                    request.Id,
                    $"/BranchManager/ReviewTransfer/{request.Id}"
                );

                // HR Officers
                var hrOfficers = await GetHROfficerUserIdentifiersAsync(request.CurrentBranch, request.RequestedBranch);
                await SendNotificationsAsync(
                    hrOfficers,
                    $"Transfer Approved by {bmLabel}",
                    $"Transfer request #{request.Id} for {request.EmployeeName} was approved by {bmLabel}. Awaiting the other branch manager.",
                    CoreNotificationType.Info,
                    request.Id,
                    $"/Transfer/Details/{request.Id}"
                );

                // Employee & Initiator
                var empAndInitiator = await GetEmployeeUserIdentifiersAsync(request.EmployeeEmail, request.EpfNumber, request.RequestedBy);
                await SendNotificationsAsync(
                    empAndInitiator,
                    $"Transfer Approved by {bmLabel}",
                    $"Transfer request #{request.Id} for {request.EmployeeName} has been approved by {bmLabel}. Awaiting other branch manager review.",
                    CoreNotificationType.Info,
                    request.Id,
                    $"/Transfer/Details/{request.Id}"
                );
            }
            else
            {
                // Rejected by Branch Manager
                var deptHeads = await GetDepartmentHeadUserIdentifiersAsync(request.CurrentBranch, request.Department);
                await SendNotificationsAsync(
                    deptHeads,
                    $"Transfer Rejected by {bmLabel} ❌",
                    $"Transfer request #{request.Id} for {request.EmployeeName} was rejected by {bmLabel}. Reason: {comments}",
                    CoreNotificationType.Rejected,
                    request.Id,
                    $"/DepartmentHead/ReviewTransfer/{request.Id}"
                );

                var allBMs = (await GetBranchManagerUserIdentifiersAsync(request.CurrentBranch))
                    .Concat(await GetBranchManagerUserIdentifiersAsync(request.RequestedBranch));
                await SendNotificationsAsync(
                    allBMs,
                    $"Transfer Rejected by {bmLabel} ❌",
                    $"Transfer request #{request.Id} for {request.EmployeeName} was rejected by {bmLabel}. Reason: {comments}",
                    CoreNotificationType.Rejected,
                    request.Id,
                    $"/BranchManager/ReviewTransfer/{request.Id}"
                );

                var hrOfficers = await GetHROfficerUserIdentifiersAsync(request.CurrentBranch, request.RequestedBranch);
                await SendNotificationsAsync(
                    hrOfficers,
                    $"Transfer Rejected by {bmLabel} ❌",
                    $"Transfer request #{request.Id} for {request.EmployeeName} was rejected by {bmLabel}. Reason: {comments}",
                    CoreNotificationType.Rejected,
                    request.Id,
                    $"/Transfer/Details/{request.Id}"
                );

                var empAndInitiator = await GetEmployeeUserIdentifiersAsync(request.EmployeeEmail, request.EpfNumber, request.RequestedBy);
                await SendNotificationsAsync(
                    empAndInitiator,
                    $"Transfer Rejected by {bmLabel} ❌",
                    $"Transfer request #{request.Id} for {request.EmployeeName} was rejected by {bmLabel}. Reason: {comments}",
                    CoreNotificationType.Rejected,
                    request.Id,
                    $"/Transfer/Details/{request.Id}"
                );
            }

            return true;
        }

        // ── Stage 4: Area Manager ─────────────────────────────────────────────
        public async Task<List<TransferRequestViewModel>> GetReviewedByAreaManagerAsync()
        {
            var entities = await _context.TransferRequests
                .Where(r => r.AreaManagerReview != null)
                .OrderByDescending(r => r.AreaManagerReviewDate)
                .ToListAsync();

            var result = new List<TransferRequestViewModel>();
            foreach (var e in entities)
            {
                if (await IsManagerialRequestAsync(e)) continue;
                result.Add(MapToViewModel(e));
            }
            return result;
        }

        public async Task<List<TransferRequestViewModel>> GetRequestsForAreaManagerAsync()
        {
            var entities = await _context.TransferRequests
                .Where(r => r.Status == DomainTransfer.TransferRequestStatus.BothBMsApproved)
                .OrderByDescending(r => r.RequestedDate)
                .ToListAsync();

            var result = new List<TransferRequestViewModel>();
            bool anyUpdated = false;
            foreach (var e in entities)
            {
                if (await IsManagerialRequestAsync(e))
                {
                    if (e.Status != DomainTransfer.TransferRequestStatus.PendingHRReview && e.Status != DomainTransfer.TransferRequestStatus.ManagerReviewed)
                    {
                        e.Status = DomainTransfer.TransferRequestStatus.PendingHRReview;
                        anyUpdated = true;
                    }
                    continue; // Exclude managerial requests from Area Manager
                }
                result.Add(MapToViewModel(e));
            }
            if (anyUpdated)
            {
                await _context.SaveChangesAsync();
            }
            return result;
        }

        public async Task<bool> AreaManagerReviewAsync(int id, bool approved, string comments)
        {
            var request = await _context.TransferRequests.FindAsync(id);
            if (request == null || request.Status != DomainTransfer.TransferRequestStatus.BothBMsApproved) return false;
            if (await IsManagerialRequestAsync(request)) return false;

            request.AreaManagerReview     = approved ? "Approved" : "Rejected";
            request.AreaManagerReviewDate = DateTime.Now;
            request.AreaManagerComments   = comments;
            request.Status = approved
                ? DomainTransfer.TransferRequestStatus.AreaManagerApproved
                : DomainTransfer.TransferRequestStatus.AreaManagerRejected;

            await _context.SaveChangesAsync();

            if (approved)
            {
                // 1. Notify HR Managers & HR Officers
                var hrRecipients = (await GetHRManagerUserIdentifiersAsync())
                    .Concat(await GetHROfficerUserIdentifiersAsync(request.CurrentBranch, request.RequestedBranch));
                await SendNotificationsAsync(
                    hrRecipients,
                    "Transfer Request Ready for HR Finalization",
                    $"Transfer request #{request.Id} for {request.EmployeeName} ({request.CurrentBranch} → {request.RequestedBranch}) has been approved by the Area Manager and is ready for HR finalization.",
                    CoreNotificationType.Info,
                    request.Id,
                    $"/HRManager/ReviewTransfer/{request.Id}"
                );

                // 2. Department Head
                var deptHeads = await GetDepartmentHeadUserIdentifiersAsync(request.CurrentBranch, request.Department);
                await SendNotificationsAsync(
                    deptHeads,
                    "Transfer Approved by Area Manager ✅",
                    $"Transfer request #{request.Id} for {request.EmployeeName} has been approved by the Area Manager and is awaiting final HR approval.",
                    CoreNotificationType.Approved,
                    request.Id,
                    $"/DepartmentHead/ReviewTransfer/{request.Id}"
                );

                // 3. Branch Managers
                var bms = (await GetBranchManagerUserIdentifiersAsync(request.CurrentBranch))
                    .Concat(await GetBranchManagerUserIdentifiersAsync(request.RequestedBranch));
                await SendNotificationsAsync(
                    bms,
                    "Transfer Approved by Area Manager ✅",
                    $"Transfer request #{request.Id} for {request.EmployeeName} has been approved by the Area Manager.",
                    CoreNotificationType.Approved,
                    request.Id,
                    $"/BranchManager/ReviewTransfer/{request.Id}"
                );

                // 4. Employee & Initiator
                var empAndInitiator = await GetEmployeeUserIdentifiersAsync(request.EmployeeEmail, request.EpfNumber, request.RequestedBy);
                await SendNotificationsAsync(
                    empAndInitiator,
                    "Transfer Approved by Area Manager ✅",
                    $"Transfer request #{request.Id} for {request.EmployeeName} has been approved by the Area Manager and is now awaiting final HR approval.",
                    CoreNotificationType.Approved,
                    request.Id,
                    $"/Transfer/Details/{request.Id}"
                );
            }
            else
            {
                // Rejection notifications to all stakeholders
                var allRecipients = (await GetDepartmentHeadUserIdentifiersAsync(request.CurrentBranch, request.Department))
                    .Concat(await GetBranchManagerUserIdentifiersAsync(request.CurrentBranch))
                    .Concat(await GetBranchManagerUserIdentifiersAsync(request.RequestedBranch))
                    .Concat(await GetHROfficerUserIdentifiersAsync(request.CurrentBranch, request.RequestedBranch))
                    .Concat(await GetEmployeeUserIdentifiersAsync(request.EmployeeEmail, request.EpfNumber, request.RequestedBy));

                await SendNotificationsAsync(
                    allRecipients,
                    "Transfer Rejected by Area Manager ❌",
                    $"Transfer request #{request.Id} for {request.EmployeeName} was rejected by the Area Manager. Reason: {comments}",
                    CoreNotificationType.Rejected,
                    request.Id,
                    $"/Transfer/Details/{request.Id}"
                );
            }

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
                .Where(r => r.Status == DomainTransfer.TransferRequestStatus.AreaManagerApproved ||
                            r.Status == DomainTransfer.TransferRequestStatus.PendingHRReview)
                .OrderByDescending(r => r.RequestedDate)
                .ToListAsync();

            var result = new List<TransferRequestViewModel>();
            bool anyUpdated = false;
            foreach (var r in entities)
            {
                if (r.Status == DomainTransfer.TransferRequestStatus.PendingHRReview)
                {
                    bool isManager = await IsManagerialEmployeeAsync(r.EmployeeEmail, r.EpfNumber, r.CurrentDesignation, null, r.Department);
                    if (!isManager)
                    {
                        r.Status = DomainTransfer.TransferRequestStatus.Pending;
                        anyUpdated = true;
                        continue;
                    }
                }
                result.Add(MapToViewModel(r));
            }

            if (anyUpdated)
            {
                await _context.SaveChangesAsync();
            }

            return result;
        }

        public async Task<bool> HRManagerMarkAsReviewedAsync(int id, string comments, string reviewerEmail)
        {
            var request = await _context.TransferRequests.FindAsync(id);
            if (request == null || request.Status != DomainTransfer.TransferRequestStatus.PendingHRReview) return false;

            request.HRManagerReview     = "Reviewed";
            request.HRManagerReviewDate = DateTime.Now;
            request.HRManagerComments   = comments;
            request.Status = DomainTransfer.TransferRequestStatus.ManagerReviewed;

            await _context.SaveChangesAsync();

            // Notify Employee & Initiator
            var empIds = await GetEmployeeUserIdentifiersAsync(request.EmployeeEmail, request.EpfNumber, request.RequestedBy);
            await SendNotificationsAsync(
                empIds,
                "Transfer Notice Acknowledged by HR 👁️",
                $"Your transfer notice #{request.Id} ({request.CurrentBranch} → {request.RequestedBranch}) has been seen and acknowledged by the HR Manager. Further transfer proceedings will take place outside of this system.",
                CoreNotificationType.Info,
                request.Id,
                $"/Transfer/Details/{request.Id}"
            );

            return true;
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

            if (approved)
            {
                // 1. Employee
                var empIds = await GetEmployeeUserIdentifiersAsync(request.EmployeeEmail, request.EpfNumber, request.RequestedBy);
                await SendNotificationsAsync(
                    empIds,
                    "Your Transfer Has Been Approved & Finalized ✅",
                    $"Your transfer request #{request.Id} from {request.CurrentBranch} to {request.RequestedBranch} has been fully approved and finalized by HR.",
                    CoreNotificationType.Approved,
                    request.Id,
                    $"/Transfer/Details/{request.Id}"
                );

                // 2. Department Head
                var deptHeads = await GetDepartmentHeadUserIdentifiersAsync(request.CurrentBranch, request.Department);
                await SendNotificationsAsync(
                    deptHeads,
                    "Transfer Finalized by HR ✅",
                    $"Transfer request #{request.Id} for {request.EmployeeName} from {request.CurrentBranch} ({request.Department}) to {request.RequestedBranch} has been fully approved and finalized by HR.",
                    CoreNotificationType.Approved,
                    request.Id,
                    $"/DepartmentHead/ReviewTransfer/{request.Id}"
                );

                // 3. Current & Target Branch Managers
                var bms = (await GetBranchManagerUserIdentifiersAsync(request.CurrentBranch))
                    .Concat(await GetBranchManagerUserIdentifiersAsync(request.RequestedBranch));
                await SendNotificationsAsync(
                    bms,
                    "Transfer Finalized by HR ✅",
                    $"Transfer request #{request.Id} for {request.EmployeeName} from {request.CurrentBranch} to {request.RequestedBranch} has been finalized and approved by HR.",
                    CoreNotificationType.Approved,
                    request.Id,
                    $"/BranchManager/ReviewTransfer/{request.Id}"
                );

                // 4. Area Manager
                var areaManagers = await GetAreaManagerUserIdentifiersAsync(request.CurrentBranch, request.RequestedBranch);
                await SendNotificationsAsync(
                    areaManagers,
                    "Transfer Finalized by HR ✅",
                    $"Transfer request #{request.Id} for {request.EmployeeName} has been fully approved and finalized by HR.",
                    CoreNotificationType.Approved,
                    request.Id,
                    $"/AreaManager/ReviewTransfer/{request.Id}"
                );

                // 5. HR Officers & Initiator
                var hrOfficers = await GetHROfficerUserIdentifiersAsync(request.CurrentBranch, request.RequestedBranch);
                await SendNotificationsAsync(
                    hrOfficers,
                    "Transfer Fully Approved & Finalized ✅",
                    $"Transfer request #{request.Id} for {request.EmployeeName} has been fully approved and finalized by HR.",
                    CoreNotificationType.Approved,
                    request.Id,
                    $"/Transfer/Details/{request.Id}"
                );
            }
            else
            {
                // Rejection notifications
                var allRecipients = (await GetDepartmentHeadUserIdentifiersAsync(request.CurrentBranch, request.Department))
                    .Concat(await GetBranchManagerUserIdentifiersAsync(request.CurrentBranch))
                    .Concat(await GetBranchManagerUserIdentifiersAsync(request.RequestedBranch))
                    .Concat(await GetAreaManagerUserIdentifiersAsync(request.CurrentBranch, request.RequestedBranch))
                    .Concat(await GetHROfficerUserIdentifiersAsync(request.CurrentBranch, request.RequestedBranch))
                    .Concat(await GetEmployeeUserIdentifiersAsync(request.EmployeeEmail, request.EpfNumber, request.RequestedBy));

                await SendNotificationsAsync(
                    allRecipients,
                    "Transfer Rejected at HR Finalization ❌",
                    $"Transfer request #{request.Id} for {request.EmployeeName} was rejected at HR finalization. Reason: {comments}",
                    CoreNotificationType.Rejected,
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
            if (string.IsNullOrWhiteSpace(email)) return new List<TransferRequestViewModel>();

            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserName == email || u.Email == email);
            HRMS.Domain.Entities.Core.Employee? emp = null;
            if (user?.EmployeeId.HasValue == true)
            {
                emp = await _context.Employees.FirstOrDefaultAsync(e => e.Id == user.EmployeeId.Value);
            }
            if (emp == null && !string.IsNullOrEmpty(user?.Email))
            {
                emp = await _context.Employees.FirstOrDefaultAsync(e => e.Email == user.Email);
            }
            if (emp == null)
            {
                emp = await _context.Employees.FirstOrDefaultAsync(e => e.Email == email);
            }

            var idSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { email };
            if (!string.IsNullOrEmpty(user?.UserName)) idSet.Add(user.UserName);
            if (!string.IsNullOrEmpty(user?.Email)) idSet.Add(user.Email);
            if (!string.IsNullOrEmpty(emp?.Email)) idSet.Add(emp.Email);

            var epf = emp?.EPFNumber ?? user?.EpfNumber;
            var fullName = emp?.FullName ?? user?.FullName;

            var entities = await _context.TransferRequests
                .Where(r => idSet.Contains(r.RequestedBy) ||
                            idSet.Contains(r.EmployeeEmail) ||
                            (!string.IsNullOrEmpty(epf) && r.EpfNumber == epf) ||
                            (!string.IsNullOrEmpty(fullName) && r.EmployeeName == fullName))
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

        // ── Managerial Role Detection Helpers ─────────────────────────────────
        public static bool IsManagerialTitle(string? title)
        {
            if (string.IsNullOrWhiteSpace(title)) return false;
            var t = title.Trim().ToLower();
            return t.Contains("department head") ||
                   t.Contains("dept head") ||
                   t.Contains("head of") ||
                   t.Contains("head") ||
                   t.Contains("branch manager") ||
                   t.Contains("area manager") ||
                   t.Contains("welfare manager") ||
                   t.Contains("welfare head") ||
                   t.Contains("regional manager") ||
                   t.Contains("general manager") ||
                   t.Contains("assistant manager") ||
                   t.Contains("manager") ||
                   t.Equals("managerial") ||
                   t.Equals("management") ||
                   t.Contains("director") ||
                   t.Contains("chief") ||
                   t.Contains("officer in charge") ||
                   t.Contains("executive officer") ||
                   t.Contains("supervisor") ||
                   t.Contains("lead") ||
                   t.StartsWith("duty");
        }

        public static bool IsManagerialDept(string? name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            var n = name.Trim().ToLower();
            return n.Equals("managerial") ||
                   n.Equals("management") ||
                   n.Contains("managerial") ||
                   n.Contains("management");
        }

        public async Task<bool> IsManagerialRequestAsync(DomainTransfer.TransferRequest request)
        {
            if (request == null) return false;
            if (request.Status == DomainTransfer.TransferRequestStatus.PendingHRReview ||
                request.Status == DomainTransfer.TransferRequestStatus.ManagerReviewed)
            {
                var isActuallyManager = await IsManagerialEmployeeAsync(request.EmployeeEmail, request.EpfNumber, request.CurrentDesignation, null, request.Department);
                if (isActuallyManager) return true;
                return request.Status == DomainTransfer.TransferRequestStatus.ManagerReviewed;
            }

            return await IsManagerialEmployeeAsync(request.EmployeeEmail, request.EpfNumber, request.CurrentDesignation, null, request.Department);
        }

        public async Task<bool> IsManagerialEmployeeAsync(string? email, string? epf, string? designation, string? requestedByRole, string? department = null)
        {
            if (!string.IsNullOrWhiteSpace(requestedByRole) &&
                !requestedByRole.Equals("HR Manager", StringComparison.OrdinalIgnoreCase) &&
                !requestedByRole.Equals("HR Officer", StringComparison.OrdinalIgnoreCase) &&
                !requestedByRole.Equals("Admin", StringComparison.OrdinalIgnoreCase) &&
                IsManagerialTitle(requestedByRole))
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(designation) && IsManagerialTitle(designation)) return true;
            if (!string.IsNullOrWhiteSpace(department) && IsManagerialDept(department)) return true;

            var eKey = email?.Trim().ToLower();
            var epfKey = epf?.Trim().ToLower();

            if (!string.IsNullOrEmpty(epfKey) && epfKey.StartsWith("duty", StringComparison.OrdinalIgnoreCase)) return true;
            if (!string.IsNullOrEmpty(eKey) && (eKey.StartsWith("duty-") || eKey.StartsWith("dh.") || eKey.StartsWith("bm.") || eKey.StartsWith("am.") || eKey.Contains("welfare") || eKey.Contains("admin") || eKey.Contains("manager") || eKey.Contains("head"))) return true;

            var users = await _context.Users
                .Where(u => (!string.IsNullOrEmpty(eKey) && ((u.Email != null && u.Email.ToLower() == eKey) || (u.UserName != null && u.UserName.ToLower() == eKey))) ||
                            (!string.IsNullOrEmpty(epfKey) && u.EpfNumber != null && u.EpfNumber.ToLower() == epfKey))
                .ToListAsync();

            foreach (var user in users)
            {
                if (IsManagerialTitle(user.Designation)) return true;
                if (IsManagerialDept(user.Department)) return true;
                if (!string.IsNullOrEmpty(user.EpfNumber) && user.EpfNumber.StartsWith("DUTY", StringComparison.OrdinalIgnoreCase)) return true;
                if (!string.IsNullOrEmpty(user.UserName) && (user.UserName.StartsWith("dh.") || user.UserName.StartsWith("bm.") || user.UserName.StartsWith("am.") || user.UserName.Contains("welfare") || user.UserName.Contains("admin") || user.UserName.Contains("manager") || user.UserName.Contains("head"))) return true;

                var roleNames = await (from ur in _context.UserRoles
                                       join r in _context.Roles on ur.RoleId equals r.Id
                                       where ur.UserId == user.Id
                                       select r.Name).ToListAsync();

                if (roleNames.Any(r => r == "Department Head" || r == "Branch Manager" || r == "Area Manager" || r == "Welfare Manager" || r == "Admin" || r == "HR Manager"))
                    return true;

                if (user.EmployeeId.HasValue)
                {
                    var linkedEmp = await _context.Employees
                        .Include(e => e.Designation)
                        .Include(e => e.Department)
                        .FirstOrDefaultAsync(e => e.Id == user.EmployeeId.Value);
                    if (linkedEmp != null)
                    {
                        if (IsManagerialTitle(linkedEmp.Designation?.Title)) return true;
                        if (IsManagerialDept(linkedEmp.Department?.Name)) return true;
                        if (linkedEmp.NIC.StartsWith("DUTY", StringComparison.OrdinalIgnoreCase) || linkedEmp.EPFNumber.StartsWith("DUTY", StringComparison.OrdinalIgnoreCase)) return true;
                    }
                }
            }

            var emp = await _context.Employees
                .Include(e => e.Designation)
                .Include(e => e.Department)
                .FirstOrDefaultAsync(e => (!string.IsNullOrEmpty(eKey) && e.Email != null && e.Email.ToLower() == eKey) ||
                                          (!string.IsNullOrEmpty(epfKey) && e.EPFNumber != null && e.EPFNumber.ToLower() == epfKey));

            if (emp != null)
            {
                if (IsManagerialTitle(emp.Designation?.Title)) return true;
                if (IsManagerialDept(emp.Department?.Name)) return true;
                if (emp.NIC.StartsWith("DUTY", StringComparison.OrdinalIgnoreCase) || emp.EPFNumber.StartsWith("DUTY", StringComparison.OrdinalIgnoreCase)) return true;

                var linkedUser = await _context.Users.FirstOrDefaultAsync(u => (emp.Id > 0 && u.EmployeeId == emp.Id) || (!string.IsNullOrEmpty(u.Email) && !string.IsNullOrEmpty(emp.Email) && u.Email.ToLower() == emp.Email.ToLower()));
                if (linkedUser != null)
                {
                    if (IsManagerialTitle(linkedUser.Designation)) return true;
                    if (IsManagerialDept(linkedUser.Department)) return true;
                    var roleNames = await (from ur in _context.UserRoles
                                           join r in _context.Roles on ur.RoleId equals r.Id
                                           where ur.UserId == linkedUser.Id
                                           select r.Name).ToListAsync();
                    if (roleNames.Any(r => r == "Department Head" || r == "Branch Manager" || r == "Area Manager" || r == "Welfare Manager" || r == "Admin" || r == "HR Manager"))
                        return true;
                }
            }

            return false;
        }

        // ── Notification Helpers ─────────────────────────────────────────────
        private async Task<List<string>> GetDepartmentHeadUserIdentifiersAsync(string? branchName, string? deptName)
        {
            if (string.IsNullOrWhiteSpace(branchName) || string.IsNullOrWhiteSpace(deptName))
                return new List<string>();

            var role = await _context.Roles.FirstOrDefaultAsync(r => r.Name == "Department Head");
            if (role == null) return new List<string>();

            var bKey = branchName.Trim().ToLower();
            var dKey = deptName.Trim().ToLower();

            var users = await (from ur in _context.UserRoles
                               join u in _context.Users on ur.UserId equals u.Id
                               join e in _context.Employees on u.EmployeeId equals e.Id into empGroup
                               from emp in empGroup.DefaultIfEmpty()
                               join b in _context.Branches on emp.BranchId equals b.Id into branchGroup
                               from br in branchGroup.DefaultIfEmpty()
                               join d in _context.Departments on emp.DepartmentId equals d.Id into deptGroup
                               from dp in deptGroup.DefaultIfEmpty()
                               where ur.RoleId == role.Id
                               select new
                               {
                                   u.Id,
                                   u.UserName,
                                   u.Email,
                                   uBranch = u.Branch,
                                   uDept = u.Department,
                                   empBranch = br != null ? br.Name : "",
                                   empDept = dp != null ? dp.Name : ""
                               }).ToListAsync();

            return users
                .Where(x =>
                    ((!string.IsNullOrEmpty(x.uBranch) && x.uBranch.Trim().ToLower() == bKey) ||
                     (!string.IsNullOrEmpty(x.empBranch) && x.empBranch.Trim().ToLower() == bKey))
                    &&
                    ((!string.IsNullOrEmpty(x.uDept) && x.uDept.Trim().ToLower() == dKey) ||
                     (!string.IsNullOrEmpty(x.empDept) && x.empDept.Trim().ToLower() == dKey)))
                .Select(x => x.Id)
                .Distinct()
                .ToList();
        }

        private async Task<List<string>> GetBranchManagerUserIdentifiersAsync(string? branchName)
        {
            if (string.IsNullOrWhiteSpace(branchName))
                return new List<string>();

            var role = await _context.Roles.FirstOrDefaultAsync(r => r.Name == "Branch Manager");
            if (role == null) return new List<string>();

            var bKey = branchName.Trim().ToLower();

            var users = await (from ur in _context.UserRoles
                               join u in _context.Users on ur.UserId equals u.Id
                               join e in _context.Employees on u.EmployeeId equals e.Id into empGroup
                               from emp in empGroup.DefaultIfEmpty()
                               join b in _context.Branches on emp.BranchId equals b.Id into branchGroup
                               from br in branchGroup.DefaultIfEmpty()
                               where ur.RoleId == role.Id
                               select new
                               {
                                   u.Id,
                                   u.UserName,
                                   u.Email,
                                   uBranch = u.Branch,
                                   empBranch = br != null ? br.Name : ""
                               }).ToListAsync();

            return users
                .Where(x =>
                    (!string.IsNullOrEmpty(x.uBranch) && x.uBranch.Trim().ToLower() == bKey) ||
                    (!string.IsNullOrEmpty(x.empBranch) && x.empBranch.Trim().ToLower() == bKey))
                .Select(x => x.Id)
                .Distinct()
                .ToList();
        }

        private async Task<List<string>> GetAreaManagerUserIdentifiersAsync(string? branch1, string? branch2 = null)
        {
            var role = await _context.Roles.FirstOrDefaultAsync(r => r.Name == "Area Manager");
            if (role == null) return new List<string>();

            var branchIds = new List<int>();
            if (!string.IsNullOrWhiteSpace(branch1))
            {
                var b1 = await _context.Branches.FirstOrDefaultAsync(b => b.Name.ToLower() == branch1.Trim().ToLower());
                if (b1 != null) branchIds.Add(b1.Id);
            }
            if (!string.IsNullOrWhiteSpace(branch2))
            {
                var b2 = await _context.Branches.FirstOrDefaultAsync(b => b.Name.ToLower() == branch2.Trim().ToLower());
                if (b2 != null) branchIds.Add(b2.Id);
            }

            var users = await (from ur in _context.UserRoles
                               join u in _context.Users on ur.UserId equals u.Id
                               where ur.RoleId == role.Id
                               select new { u.Id, u.UserName, u.ManagedBranches })
                              .ToListAsync();

            var result = new List<string>();
            foreach (var u in users)
            {
                if (string.IsNullOrWhiteSpace(u.ManagedBranches))
                {
                    result.Add(u.Id);
                }
                else
                {
                    var managedIds = u.ManagedBranches.Split(',')
                        .Select(s => int.TryParse(s.Trim(), out var id) ? id : 0)
                        .Where(id => id > 0)
                        .ToHashSet();

                    if (!branchIds.Any() || branchIds.Any(bid => managedIds.Contains(bid)))
                    {
                        result.Add(u.Id);
                    }
                }
            }

            return result.Distinct().ToList();
        }

        private async Task<List<string>> GetHROfficerUserIdentifiersAsync(string? branch1, string? branch2 = null)
        {
            var role = await _context.Roles.FirstOrDefaultAsync(r => r.Name == "HR Officer");
            if (role == null) return new List<string>();

            var branchIds = new List<int>();
            if (!string.IsNullOrWhiteSpace(branch1))
            {
                var b1 = await _context.Branches.FirstOrDefaultAsync(b => b.Name.ToLower() == branch1.Trim().ToLower());
                if (b1 != null) branchIds.Add(b1.Id);
            }
            if (!string.IsNullOrWhiteSpace(branch2))
            {
                var b2 = await _context.Branches.FirstOrDefaultAsync(b => b.Name.ToLower() == branch2.Trim().ToLower());
                if (b2 != null) branchIds.Add(b2.Id);
            }

            var b1Key = branch1?.Trim().ToLower() ?? "";
            var b2Key = branch2?.Trim().ToLower() ?? "";

            var users = await (from ur in _context.UserRoles
                               join u in _context.Users on ur.UserId equals u.Id
                               where ur.RoleId == role.Id
                               select new { u.Id, u.UserName, u.Branch, u.ManagedBranches })
                              .ToListAsync();

            var result = new List<string>();
            foreach (var u in users)
            {
                if (string.IsNullOrWhiteSpace(u.ManagedBranches))
                {
                    if (string.IsNullOrWhiteSpace(u.Branch) ||
                        (!string.IsNullOrEmpty(b1Key) && u.Branch.Trim().ToLower() == b1Key) ||
                        (!string.IsNullOrEmpty(b2Key) && u.Branch.Trim().ToLower() == b2Key))
                    {
                        result.Add(u.Id);
                    }
                }
                else
                {
                    var managedIds = u.ManagedBranches.Split(',')
                        .Select(s => int.TryParse(s.Trim(), out var id) ? id : 0)
                        .Where(id => id > 0)
                        .ToHashSet();

                    if (!branchIds.Any() || branchIds.Any(bid => managedIds.Contains(bid)))
                    {
                        result.Add(u.Id);
                    }
                }
            }

            return result.Distinct().ToList();
        }

        private async Task<List<string>> GetHRManagerUserIdentifiersAsync()
        {
            var role = await _context.Roles.FirstOrDefaultAsync(r => r.Name == "HR Manager");
            if (role == null) return new List<string>();

            return await (from ur in _context.UserRoles
                          join u in _context.Users on ur.UserId equals u.Id
                          where ur.RoleId == role.Id
                          select u.Id)
                         .Distinct()
                         .ToListAsync();
        }

        private async Task<List<string>> GetEmployeeUserIdentifiersAsync(string? email, string? epf, string? requestedBy)
        {
            var eKey = email?.Trim().ToLower() ?? "";
            var epfKey = epf?.Trim().ToLower() ?? "";
            var reqKey = requestedBy?.Trim().ToLower() ?? "";

            var userIds = await _context.Users
                .Where(u => (!string.IsNullOrEmpty(eKey) && ((u.Email != null && u.Email.ToLower() == eKey) || (u.UserName != null && u.UserName.ToLower() == eKey))) ||
                            (!string.IsNullOrEmpty(epfKey) && u.EpfNumber != null && u.EpfNumber.ToLower() == epfKey) ||
                            (!string.IsNullOrEmpty(reqKey) && ((u.UserName != null && u.UserName.ToLower() == reqKey) || (u.Email != null && u.Email.ToLower() == reqKey))))
                .Select(u => u.Id)
                .Distinct()
                .ToListAsync();

            if (!userIds.Any())
            {
                if (!string.IsNullOrWhiteSpace(email)) userIds.Add(email.Trim());
                else if (!string.IsNullOrWhiteSpace(requestedBy)) userIds.Add(requestedBy.Trim());
            }

            return userIds.Distinct().ToList();
        }

        private async Task SendNotificationsAsync(IEnumerable<string> recipientIdentifiers, string title, string message, CoreNotificationType type, int transferRequestId, string targetUrl = "")
        {
            var distinctRecipients = recipientIdentifiers
                .Where(r => !string.IsNullOrWhiteSpace(r))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var recipient in distinctRecipients)
            {
                try
                {
                    await _notificationService.CreateNotificationAsync(recipient, title, message, type, transferRequestId, targetUrl);
                }
                catch
                {
                    // Ignore individual notification failure to prevent blocking business workflow
                }
            }
        }
    }
}
