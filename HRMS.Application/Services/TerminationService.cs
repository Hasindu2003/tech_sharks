using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HRMS.Application.Models;
using HRMS.Domain.Entities.Core;
using HRMS.Domain.Entities.Termination;
using HRMS.Domain.Common;
using HRMS.Infrastructure.Persistence;
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

        // ── Stage 2: Department Head Clearances in Branch ──
        Task<List<TerminationRequestViewModel>> GetPendingForDeptHeadAsync(string branch, string department);
        Task<List<TerminationRequestViewModel>> GetReviewedByDeptHeadAsync(string branch, string department);
        Task<(bool Success, string? ErrorMessage)> DeptHeadReviewAsync(int requestId, string departmentName, string status, string comments, string reviewerUserId, string reviewerName, string reviewerEmail);

        // ── Stage 3: Branch Manager Review ──
        Task<List<TerminationRequestViewModel>> GetPendingForBranchManagerAsync(string branch);
        Task<List<TerminationRequestViewModel>> GetReviewedByBranchManagerAsync(string branch);
        Task<(bool Success, string? ErrorMessage)> BranchManagerReviewAsync(int id, bool approved, string comments, string approverEmail);

        // ── Stage 4: Area Manager Review ──
        Task<List<TerminationRequestViewModel>> GetPendingForAreaManagerAsync(List<int>? managedBranchIds = null, string? branch = null);
        Task<List<TerminationRequestViewModel>> GetReviewedByAreaManagerAsync(List<int>? managedBranchIds = null, string? branch = null);
        Task<(bool Success, string? ErrorMessage)> AreaManagerReviewAsync(int id, bool approved, string comments, string approverEmail);

        // ── Stage 5: HR Officer Finalization ──
        Task<List<TerminationRequestViewModel>> GetPendingForHROfficerAsync(List<int>? managedBranchIds = null);
        Task<List<TerminationRequestViewModel>> GetReviewedByHROfficerAsync(List<int>? managedBranchIds = null);
        Task<(bool Success, string? ErrorMessage)> FinalizeTerminationAsync(int id, bool approved, string comments, string hrEmail);
    }

    public class TerminationService : ITerminationService
    {
        private readonly ApplicationDbContext _context;
        private readonly INotificationService _notificationService;

        public TerminationService(ApplicationDbContext context, INotificationService notificationService)
        {
            _context = context;
            _notificationService = notificationService;
        }

        // ── Fuzzy Helpers ──
        private static bool MatchBranch(string a, string b)
        {
            if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b)) return false;
            var cleanA = a.ToLower().Replace("branch", "").Trim();
            var cleanB = b.ToLower().Replace("branch", "").Trim();
            return cleanA == cleanB || cleanA.Contains(cleanB) || cleanB.Contains(cleanA);
        }

        private static bool MatchDept(string a, string b)
        {
            if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b)) return false;
            var cleanA = a.ToLower().Replace("department", "").Replace("dept", "").Trim();
            var cleanB = b.ToLower().Replace("department", "").Replace("dept", "").Trim();
            return cleanA == cleanB || cleanA.Contains(cleanB) || cleanB.Contains(cleanA);
        }

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
                Status = TerminationRequestStatus.Draft,
                InitiatedBy = request.InitiatedBy,
                InitiatedByRole = request.InitiatedByRole,
                CreatedDate = DateTime.Now,
                LastModifiedDate = DateTime.Now
            };

            _context.TerminationRequests.Add(entity);
            await _context.SaveChangesAsync();

            // Initialize branch department reviews
            await InitializeBranchDepartmentReviewsAsync(entity);

            return entity.Id;
        }

        public async Task<bool> UpdateTerminationRequestAsync(TerminationRequestViewModel request)
        {
            var entity = await _context.TerminationRequests
                .Include(t => t.DepartmentReviews)
                .FirstOrDefaultAsync(t => t.Id == request.Id);

            if (entity == null || entity.Status != TerminationRequestStatus.Draft)
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

        private static bool IsManagerialDept(string? name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            var norm = name.Trim().ToLower();
            return norm == "managerial" || norm == "management" || norm.StartsWith("managerial") || norm.StartsWith("management");
        }

        private async Task InitializeBranchDepartmentReviewsAsync(TerminationRequest entity)
        {
            if (string.IsNullOrWhiteSpace(entity.Branch)) return;

            var branchName = entity.Branch.Trim().ToLower().Replace("branch", "").Trim();
            var branch = await _context.Branches.FirstOrDefaultAsync(b => b.Name.ToLower() == branchName || b.Name.ToLower().Replace("branch", "").Trim() == branchName);

            var deptNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // 1. Get departments linked to this branch via BranchDepartments (excluding Managerial)
            if (branch != null)
            {
                var branchDepts = await _context.BranchDepartments
                    .Where(bd => bd.BranchId == branch.Id)
                    .Include(bd => bd.Department)
                    .Select(bd => bd.Department.Name)
                    .ToListAsync();

                foreach (var d in branchDepts)
                {
                    if (!string.IsNullOrWhiteSpace(d) && !IsManagerialDept(d))
                        deptNames.Add(d.Trim());
                }
            }

            // 2. Also check active Department Head accounts assigned to this branch
            var dhRole = await _context.Roles.FirstOrDefaultAsync(r => r.Name == "Department Head");
            if (dhRole != null)
            {
                var dhUsers = await (from ur in _context.UserRoles
                                     join u in _context.Users on ur.UserId equals u.Id
                                     join e in _context.Employees on u.EmployeeId equals e.Id into empGroup
                                     from emp in empGroup.DefaultIfEmpty()
                                     join b in _context.Branches on emp.BranchId equals b.Id into branchGroup
                                     from br in branchGroup.DefaultIfEmpty()
                                     join d in _context.Departments on emp.DepartmentId equals d.Id into deptGroup
                                     from dp in deptGroup.DefaultIfEmpty()
                                     where ur.RoleId == dhRole.Id
                                     select new
                                     {
                                         uBranch = u.Branch,
                                         uDept = u.Department,
                                         empBranch = br != null ? br.Name : "",
                                         empDept = dp != null ? dp.Name : ""
                                     }).ToListAsync();

                var matchingDHs = dhUsers.Where(x =>
                    (!string.IsNullOrEmpty(x.uBranch) && (x.uBranch.Trim().ToLower().Replace("branch", "").Trim() == branchName || x.uBranch.ToLower().Contains(branchName) || branchName.Contains(x.uBranch.ToLower()))) ||
                    (!string.IsNullOrEmpty(x.empBranch) && (x.empBranch.Trim().ToLower().Replace("branch", "").Trim() == branchName || x.empBranch.ToLower().Contains(branchName) || branchName.Contains(x.empBranch.ToLower()))));

                foreach (var dh in matchingDHs)
                {
                    var dName = !string.IsNullOrWhiteSpace(dh.uDept) ? dh.uDept : dh.empDept;
                    if (!string.IsNullOrWhiteSpace(dName) && !IsManagerialDept(dName))
                        deptNames.Add(dName.Trim());
                }
            }

            // 3. Fallback: If no departments were found, add employee's own department or standard depts (excluding Managerial)
            if (!deptNames.Any() && !string.IsNullOrWhiteSpace(entity.Department) && !IsManagerialDept(entity.Department))
            {
                deptNames.Add(entity.Department.Trim());
            }
            if (!deptNames.Any())
            {
                var allDepts = await _context.Departments
                    .Where(d => d.Name.ToLower() != "managerial" && d.Name.ToLower() != "management")
                    .Select(d => d.Name.Trim())
                    .Distinct()
                    .ToListAsync();
                foreach (var d in (allDepts.Any() ? allDepts : new List<string> { "Operations", "Finance", "HR", "IT" }))
                {
                    if (!IsManagerialDept(d))
                        deptNames.Add(d);
                }
            }

            // Explicitly remove any managerial department
            deptNames.RemoveWhere(IsManagerialDept);

            // Clean up any previous records for this request
            var existingReviews = await _context.TerminationDepartmentReviews
                .Where(r => r.TerminationRequestId == entity.Id)
                .ToListAsync();
            if (existingReviews.Any())
            {
                _context.TerminationDepartmentReviews.RemoveRange(existingReviews);
            }

            foreach (var dept in deptNames)
            {
                _context.TerminationDepartmentReviews.Add(new TerminationDepartmentReview
                {
                    TerminationRequestId = entity.Id,
                    DepartmentName = dept,
                    Status = "Pending"
                });
            }

            await _context.SaveChangesAsync();
        }

        public async Task<(bool Success, string? ErrorMessage)> ValidateAndSubmitAsync(int id)
        {
            var entity = await _context.TerminationRequests
                .Include(t => t.Documents)
                .Include(t => t.DepartmentReviews)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (entity == null)
                return (false, "Termination request not found.");

            if (entity.Status != TerminationRequestStatus.Draft)
                return (false, "Only requests in 'Draft' status can be submitted.");

            if (!entity.Documents.Any())
                return (false, "At least one supporting document must be attached before submission.");

            if (entity.HasOutstandingLoans && !entity.HasOverridePermission)
                return (false, "Employee has outstanding loan balances. The termination cannot proceed until obligations are cleared, or a senior management override is provided.");

            if (entity.EffectiveTerminationDate.Date < SriLankaTime.Today)
                return (false, "Effective termination date cannot be in the past.");

            if (entity.IsLoanGuarantor && !entity.HasOverridePermission)
                return (false, "Employee is listed as a loan guarantor for another employee. The termination cannot proceed until the guarantee is released, or a senior management override is provided.");

            await InitializeBranchDepartmentReviewsAsync(entity);

            if (entity.DepartmentReviews.Any())
            {
                entity.Status = TerminationRequestStatus.SubmittedForApproval;
                entity.LastModifiedDate = DateTime.Now;
                await _context.SaveChangesAsync();

                // ── Notifications: Stage 1 Initiation Complete ──
                // 1. Notify Department Heads in Branch (excluding Managerial)
                var deptHeadIds = await GetDepartmentHeadUserIdentifiersAsync(entity.Branch);
                await SendNotificationsAsync(
                    deptHeadIds,
                    "Action Required: New Termination Clearance Request ⚠️",
                    $"A new termination request #{entity.Id} for {entity.EmployeeName} ({entity.EpfNumber} - {entity.Branch}) has been initiated. Please review and provide department clearance.",
                    CoreNotificationType.Info,
                    entity.Id,
                    $"/DepartmentHead/ReviewTermination/{entity.Id}"
                );

                // 2. Notify Initiator / Assigned HR Officers
                var hrOfficerIds = await GetHROfficerUserIdentifiersAsync(entity.Branch);
                await SendNotificationsAsync(
                    hrOfficerIds,
                    "Termination Request Submitted for Approvals",
                    $"Termination request #{entity.Id} for {entity.EmployeeName} has been submitted for branch department head clearances.",
                    CoreNotificationType.Info,
                    entity.Id,
                    $"/Termination/Details?id={entity.Id}"
                );
            }
            else
            {
                // If branch has no non-managerial departments needing clearance, advance directly to Stage 2 (BM)
                entity.Status = TerminationRequestStatus.DeptHeadsApproved;
                entity.LastModifiedDate = DateTime.Now;
                await _context.SaveChangesAsync();

                var bmIds = await GetBranchManagerUserIdentifiersAsync(entity.Branch);
                await SendNotificationsAsync(
                    bmIds,
                    "Action Required: Termination Request Awaiting Branch Manager Review ⚠️",
                    $"Termination request #{entity.Id} for {entity.EmployeeName} ({entity.Branch}) has been initiated and awaits your review.",
                    CoreNotificationType.Info,
                    entity.Id,
                    $"/BranchManager/ReviewTermination/{entity.Id}"
                );

                var hrOfficerIds = await GetHROfficerUserIdentifiersAsync(entity.Branch);
                await SendNotificationsAsync(
                    hrOfficerIds,
                    "Termination Request Submitted for Approvals",
                    $"Termination request #{entity.Id} for {entity.EmployeeName} has been submitted directly to Branch Manager review.",
                    CoreNotificationType.Info,
                    entity.Id,
                    $"/Termination/Details?id={entity.Id}"
                );
            }

            return (true, null);
        }

        public async Task<List<TerminationRequestViewModel>> GetTerminationRequestsAsync(string? statusFilter = null, string? search = null)
        {
            var query = _context.TerminationRequests
                .Include(t => t.Documents)
                .Include(t => t.DepartmentReviews)
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
                    t.EmployeeEmail.ToLower().Contains(s) ||
                    t.Branch.ToLower().Contains(s));
            }

            var entities = await query.OrderByDescending(t => t.CreatedDate).ToListAsync();
            return entities.Select(MapToViewModel).ToList();
        }

        public async Task<List<TerminationRequestViewModel>> GetPendingApprovalsAsync()
        {
            var entities = await _context.TerminationRequests
                .Include(t => t.Documents)
                .Include(t => t.DepartmentReviews)
                .Where(t => t.Status == TerminationRequestStatus.SubmittedForApproval ||
                            t.Status == TerminationRequestStatus.DeptHeadsApproved ||
                            t.Status == TerminationRequestStatus.BMApproved ||
                            t.Status == TerminationRequestStatus.AMApproved)
                .OrderByDescending(t => t.CreatedDate)
                .ToListAsync();

            return entities.Select(MapToViewModel).ToList();
        }

        public async Task<TerminationRequestViewModel?> GetTerminationByIdAsync(int id)
        {
            var entity = await _context.TerminationRequests
                .Include(t => t.Documents)
                .Include(t => t.DepartmentReviews)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (entity != null && !entity.DepartmentReviews.Any())
            {
                await InitializeBranchDepartmentReviewsAsync(entity);
            }

            return entity == null ? null : MapToViewModel(entity);
        }

        // ── Stage 2: Department Head Clearances ──
        public async Task<List<TerminationRequestViewModel>> GetPendingForDeptHeadAsync(string branch, string department)
        {
            if (string.IsNullOrWhiteSpace(branch) || IsManagerialDept(department)) return new List<TerminationRequestViewModel>();

            var all = await _context.TerminationRequests
                .Include(t => t.Documents)
                .Include(t => t.DepartmentReviews)
                .Where(t => t.Status == TerminationRequestStatus.SubmittedForApproval)
                .ToListAsync();

            var result = new List<TerminationRequestViewModel>();
            foreach (var r in all)
            {
                if (!MatchBranch(r.Branch, branch)) continue;

                if (!r.DepartmentReviews.Any())
                {
                    await InitializeBranchDepartmentReviewsAsync(r);
                }

                bool hasPendingForThisDept = false;
                if (!string.IsNullOrWhiteSpace(department))
                {
                    hasPendingForThisDept = r.DepartmentReviews.Any(dr => !IsManagerialDept(dr.DepartmentName) && MatchDept(dr.DepartmentName, department) && dr.Status == "Pending");
                }
                else
                {
                    hasPendingForThisDept = r.DepartmentReviews.Any(dr => !IsManagerialDept(dr.DepartmentName) && dr.Status == "Pending");
                }

                if (hasPendingForThisDept)
                {
                    result.Add(MapToViewModel(r));
                }
            }

            return result.OrderByDescending(r => r.CreatedDate).ToList();
        }

        public async Task<List<TerminationRequestViewModel>> GetReviewedByDeptHeadAsync(string branch, string department)
        {
            if (string.IsNullOrWhiteSpace(branch) || IsManagerialDept(department)) return new List<TerminationRequestViewModel>();

            var all = await _context.TerminationRequests
                .Include(t => t.Documents)
                .Include(t => t.DepartmentReviews)
                .ToListAsync();

            var result = new List<TerminationRequestViewModel>();
            foreach (var r in all)
            {
                if (!MatchBranch(r.Branch, branch)) continue;

                bool hasReviewedForThisDept = false;
                if (!string.IsNullOrWhiteSpace(department))
                {
                    hasReviewedForThisDept = r.DepartmentReviews.Any(dr => !IsManagerialDept(dr.DepartmentName) && MatchDept(dr.DepartmentName, department) && (dr.Status == "Approved" || dr.Status == "Rejected"));
                }
                else
                {
                    hasReviewedForThisDept = r.DepartmentReviews.Any(dr => !IsManagerialDept(dr.DepartmentName) && (dr.Status == "Approved" || dr.Status == "Rejected"));
                }

                if (hasReviewedForThisDept)
                {
                    result.Add(MapToViewModel(r));
                }
            }

            return result.OrderByDescending(r => r.CreatedDate).ToList();
        }

        public async Task<(bool Success, string? ErrorMessage)> DeptHeadReviewAsync(
            int requestId,
            string departmentName,
            string status,
            string comments,
            string reviewerUserId,
            string reviewerName,
            string reviewerEmail)
        {
            var entity = await _context.TerminationRequests
                .Include(t => t.DepartmentReviews)
                .FirstOrDefaultAsync(t => t.Id == requestId);

            if (entity == null) return (false, "Termination request not found.");
            if (entity.Status != TerminationRequestStatus.SubmittedForApproval)
                return (false, "This request is not currently awaiting Department Head clearance.");

            if (IsManagerialDept(departmentName))
                return (false, "Managerial department heads do not perform Stage 1 department clearances.");

            var deptReview = entity.DepartmentReviews
                .FirstOrDefault(dr => MatchDept(dr.DepartmentName, departmentName));

            if (deptReview == null)
            {
                deptReview = new TerminationDepartmentReview
                {
                    TerminationRequestId = entity.Id,
                    DepartmentName = departmentName,
                    Status = status,
                    Comments = comments,
                    ReviewerUserId = reviewerUserId,
                    ReviewerName = reviewerName,
                    ReviewerEmail = reviewerEmail,
                    ReviewDate = DateTime.Now
                };
                _context.TerminationDepartmentReviews.Add(deptReview);
            }
            else
            {
                deptReview.Status = status;
                deptReview.Comments = comments;
                deptReview.ReviewerUserId = reviewerUserId;
                deptReview.ReviewerName = reviewerName;
                deptReview.ReviewerEmail = reviewerEmail;
                deptReview.ReviewDate = DateTime.Now;
            }

            entity.LastModifiedDate = DateTime.Now;

            if (status == "Rejected")
            {
                entity.Status = TerminationRequestStatus.DeptHeadRejected;
                await _context.SaveChangesAsync();

                // Notify Initiator & HR Officers of Rejection
                var hrOfficerIds = await GetHROfficerUserIdentifiersAsync(entity.Branch);
                await SendNotificationsAsync(
                    hrOfficerIds,
                    "Termination Request Clearance Rejected ❌",
                    $"Termination request #{entity.Id} for {entity.EmployeeName} was rejected by {departmentName} Department Head ({reviewerName}). Reason: {comments}",
                    CoreNotificationType.Rejected,
                    entity.Id,
                    $"/Termination/Details?id={entity.Id}"
                );

                return (true, null);
            }

            // Status is Approved
            await _context.SaveChangesAsync();

            bool allApproved = entity.DepartmentReviews.All(dr => dr.Status == "Approved");
            if (allApproved)
            {
                entity.Status = TerminationRequestStatus.DeptHeadsApproved;
                await _context.SaveChangesAsync();

                // ── Notifications: Transition to Stage 3 (Branch Manager) ──
                var bmIds = await GetBranchManagerUserIdentifiersAsync(entity.Branch);
                await SendNotificationsAsync(
                    bmIds,
                    "Action Required: Termination Request Awaiting Branch Manager Review ⚠️",
                    $"All Department Heads in {entity.Branch} have approved termination request #{entity.Id} for {entity.EmployeeName}. Please review and submit your decision.",
                    CoreNotificationType.Info,
                    entity.Id,
                    $"/BranchManager/ReviewTermination/{entity.Id}"
                );

                var hrOfficerIds = await GetHROfficerUserIdentifiersAsync(entity.Branch);
                await SendNotificationsAsync(
                    hrOfficerIds,
                    "Department Clearances Completed ✅",
                    $"All branch department heads have approved termination request #{entity.Id} for {entity.EmployeeName}. Forwarded to Branch Manager.",
                    CoreNotificationType.Info,
                    entity.Id,
                    $"/Termination/Details?id={entity.Id}"
                );
            }
            else
            {
                int approvedCount = entity.DepartmentReviews.Count(dr => dr.Status == "Approved");
                int totalCount = entity.DepartmentReviews.Count;
                var hrOfficerIds = await GetHROfficerUserIdentifiersAsync(entity.Branch);
                await SendNotificationsAsync(
                    hrOfficerIds,
                    "Department Clearance Recorded",
                    $"{departmentName} Department Head approved termination request #{entity.Id} for {entity.EmployeeName} ({approvedCount}/{totalCount} completed).",
                    CoreNotificationType.Info,
                    entity.Id,
                    $"/Termination/Details?id={entity.Id}"
                );
            }

            return (true, null);
        }

        // ── Stage 3: Branch Manager Review ──
        public async Task<List<TerminationRequestViewModel>> GetPendingForBranchManagerAsync(string branch)
        {
            if (string.IsNullOrWhiteSpace(branch)) return new List<TerminationRequestViewModel>();

            var all = await _context.TerminationRequests
                .Include(t => t.Documents)
                .Include(t => t.DepartmentReviews)
                .Where(t => t.Status == TerminationRequestStatus.DeptHeadsApproved)
                .ToListAsync();

            return all.Where(r => MatchBranch(r.Branch, branch))
                      .OrderByDescending(r => r.CreatedDate)
                      .Select(MapToViewModel)
                      .ToList();
        }

        public async Task<List<TerminationRequestViewModel>> GetReviewedByBranchManagerAsync(string branch)
        {
            if (string.IsNullOrWhiteSpace(branch)) return new List<TerminationRequestViewModel>();

            var all = await _context.TerminationRequests
                .Include(t => t.Documents)
                .Include(t => t.DepartmentReviews)
                .Where(t => !string.IsNullOrEmpty(t.BMReview))
                .ToListAsync();

            return all.Where(r => MatchBranch(r.Branch, branch))
                      .OrderByDescending(r => r.CreatedDate)
                      .Select(MapToViewModel)
                      .ToList();
        }

        public async Task<(bool Success, string? ErrorMessage)> BranchManagerReviewAsync(int id, bool approved, string comments, string approverEmail)
        {
            var entity = await _context.TerminationRequests
                .Include(t => t.DepartmentReviews)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (entity == null) return (false, "Termination request not found.");
            if (entity.Status != TerminationRequestStatus.DeptHeadsApproved)
                return (false, "Request is not currently awaiting Branch Manager review.");

            entity.BMReview = approved ? "Approved" : "Rejected";
            entity.BMReviewDate = DateTime.Now;
            entity.BMComments = comments;
            entity.BMEmail = approverEmail;
            entity.Status = approved ? TerminationRequestStatus.BMApproved : TerminationRequestStatus.BMRejected;
            entity.LastModifiedDate = DateTime.Now;

            await _context.SaveChangesAsync();

            if (approved)
            {
                // ── Notifications: Transition to Stage 4 (Area Manager) ──
                var amIds = await GetAreaManagerUserIdentifiersAsync(entity.Branch);
                await SendNotificationsAsync(
                    amIds,
                    "Action Required: Termination Request Awaiting Area Manager Review ⚠️",
                    $"Branch Manager has approved termination request #{entity.Id} for {entity.EmployeeName} ({entity.Branch}). Please review and provide area approval.",
                    CoreNotificationType.Info,
                    entity.Id,
                    $"/AreaManager/ReviewTermination/{entity.Id}"
                );

                var hrOfficerIds = await GetHROfficerUserIdentifiersAsync(entity.Branch);
                await SendNotificationsAsync(
                    hrOfficerIds,
                    "Branch Manager Approved Termination Request ✅",
                    $"Branch Manager approved termination request #{entity.Id} for {entity.EmployeeName}. Forwarded to Area Manager.",
                    CoreNotificationType.Approved,
                    entity.Id,
                    $"/Termination/Details?id={entity.Id}"
                );
            }
            else
            {
                var hrOfficerIds = await GetHROfficerUserIdentifiersAsync(entity.Branch);
                await SendNotificationsAsync(
                    hrOfficerIds,
                    "Termination Request Rejected by Branch Manager ❌",
                    $"Branch Manager rejected termination request #{entity.Id} for {entity.EmployeeName}. Reason: {comments}",
                    CoreNotificationType.Rejected,
                    entity.Id,
                    $"/Termination/Details?id={entity.Id}"
                );
            }

            return (true, null);
        }

        // ── Stage 4: Area Manager Review ──
        public async Task<List<TerminationRequestViewModel>> GetPendingForAreaManagerAsync(List<int>? managedBranchIds = null, string? branch = null)
        {
            var query = _context.TerminationRequests
                .Include(t => t.Documents)
                .Include(t => t.DepartmentReviews)
                .Where(t => t.Status == TerminationRequestStatus.BMApproved)
                .AsQueryable();

            var all = await query.ToListAsync();
            if (managedBranchIds != null && managedBranchIds.Any())
            {
                var managedBranchNames = await _context.Branches
                    .Where(b => managedBranchIds.Contains(b.Id))
                    .Select(b => b.Name)
                    .ToListAsync();

                all = all.Where(r => managedBranchNames.Any(mb => MatchBranch(r.Branch, mb))).ToList();
            }
            else if (!string.IsNullOrWhiteSpace(branch))
            {
                all = all.Where(r => MatchBranch(r.Branch, branch)).ToList();
            }

            return all.OrderByDescending(r => r.CreatedDate).Select(MapToViewModel).ToList();
        }

        public async Task<List<TerminationRequestViewModel>> GetReviewedByAreaManagerAsync(List<int>? managedBranchIds = null, string? branch = null)
        {
            var all = await _context.TerminationRequests
                .Include(t => t.Documents)
                .Include(t => t.DepartmentReviews)
                .Where(t => !string.IsNullOrEmpty(t.AMReview))
                .ToListAsync();

            if (managedBranchIds != null && managedBranchIds.Any())
            {
                var managedBranchNames = await _context.Branches
                    .Where(b => managedBranchIds.Contains(b.Id))
                    .Select(b => b.Name)
                    .ToListAsync();

                all = all.Where(r => managedBranchNames.Any(mb => MatchBranch(r.Branch, mb))).ToList();
            }
            else if (!string.IsNullOrWhiteSpace(branch))
            {
                all = all.Where(r => MatchBranch(r.Branch, branch)).ToList();
            }

            return all.OrderByDescending(r => r.CreatedDate).Select(MapToViewModel).ToList();
        }

        public async Task<(bool Success, string? ErrorMessage)> AreaManagerReviewAsync(int id, bool approved, string comments, string approverEmail)
        {
            var entity = await _context.TerminationRequests
                .Include(t => t.DepartmentReviews)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (entity == null) return (false, "Termination request not found.");
            if (entity.Status != TerminationRequestStatus.BMApproved)
                return (false, "Request is not currently awaiting Area Manager review.");

            entity.AMReview = approved ? "Approved" : "Rejected";
            entity.AMReviewDate = DateTime.Now;
            entity.AMComments = comments;
            entity.AMEmail = approverEmail;
            entity.Status = approved ? TerminationRequestStatus.AMApproved : TerminationRequestStatus.AMRejected;
            entity.LastModifiedDate = DateTime.Now;

            await _context.SaveChangesAsync();

            if (approved)
            {
                // ── Notifications: Transition to Stage 5 (HR Finalization) ──
                var hrOfficerIds = await GetHROfficerUserIdentifiersAsync(entity.Branch);
                await SendNotificationsAsync(
                    hrOfficerIds,
                    "Action Required: Termination Ready for Final HR Action ⚠️",
                    $"Area Manager has approved termination request #{entity.Id} for {entity.EmployeeName}. Please finalize the process and complete financial clearance.",
                    CoreNotificationType.Info,
                    entity.Id,
                    $"/Termination/ReviewTermination?id={entity.Id}"
                );

                var bmIds = await GetBranchManagerUserIdentifiersAsync(entity.Branch);
                await SendNotificationsAsync(
                    bmIds,
                    "Termination Request Approved by Area Manager ✅",
                    $"Termination request #{entity.Id} for {entity.EmployeeName} was approved by Area Manager and moved to HR for finalization.",
                    CoreNotificationType.Approved,
                    entity.Id,
                    $"/BranchManager/ReviewTermination/{entity.Id}"
                );
            }
            else
            {
                var hrOfficerIds = await GetHROfficerUserIdentifiersAsync(entity.Branch);
                await SendNotificationsAsync(
                    hrOfficerIds,
                    "Termination Request Rejected by Area Manager ❌",
                    $"Area Manager rejected termination request #{entity.Id} for {entity.EmployeeName}. Reason: {comments}",
                    CoreNotificationType.Rejected,
                    entity.Id,
                    $"/Termination/Details?id={entity.Id}"
                );
            }

            return (true, null);
        }

        // ── Stage 5: HR Officer Finalization ──
        public async Task<List<TerminationRequestViewModel>> GetPendingForHROfficerAsync(List<int>? managedBranchIds = null)
        {
            var all = await _context.TerminationRequests
                .Include(t => t.Documents)
                .Include(t => t.DepartmentReviews)
                .Where(t => t.Status == TerminationRequestStatus.AMApproved || t.Status == TerminationRequestStatus.FinanceClearance)
                .ToListAsync();

            if (managedBranchIds != null && managedBranchIds.Any())
            {
                var managedBranchNames = await _context.Branches
                    .Where(b => managedBranchIds.Contains(b.Id))
                    .Select(b => b.Name)
                    .ToListAsync();

                all = all.Where(r => managedBranchNames.Any(mb => MatchBranch(r.Branch, mb))).ToList();
            }

            return all.OrderByDescending(r => r.CreatedDate).Select(MapToViewModel).ToList();
        }

        public async Task<List<TerminationRequestViewModel>> GetReviewedByHROfficerAsync(List<int>? managedBranchIds = null)
        {
            var all = await _context.TerminationRequests
                .Include(t => t.Documents)
                .Include(t => t.DepartmentReviews)
                .Where(t => t.Status == TerminationRequestStatus.Terminated ||
                            t.Status == TerminationRequestStatus.HRApproved ||
                            t.Status == TerminationRequestStatus.HRRejected ||
                            !string.IsNullOrEmpty(t.HRReview))
                .ToListAsync();

            if (managedBranchIds != null && managedBranchIds.Any())
            {
                var managedBranchNames = await _context.Branches
                    .Where(b => managedBranchIds.Contains(b.Id))
                    .Select(b => b.Name)
                    .ToListAsync();

                all = all.Where(r => managedBranchNames.Any(mb => MatchBranch(r.Branch, mb))).ToList();
            }

            return all.OrderByDescending(r => r.CreatedDate).Select(MapToViewModel).ToList();
        }

        public async Task<(bool Success, string? ErrorMessage)> FinalizeTerminationAsync(int id, bool approved, string comments, string hrEmail)
        {
            var entity = await _context.TerminationRequests
                .Include(t => t.DepartmentReviews)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (entity == null) return (false, "Termination request not found.");

            entity.HRReview = approved ? "Approved" : "Rejected";
            entity.HRReviewDate = DateTime.Now;
            entity.HRComments = comments;
            entity.HREmail = hrEmail;

            if (approved)
            {
                entity.FinanceClearanceCompleted = true;
                entity.FinanceClearanceDate = DateTime.Now;
                entity.FinanceClearanceNotes = !string.IsNullOrWhiteSpace(comments) ? comments : "Financial clearance and offboarding finalized by HR.";
                entity.Status = TerminationRequestStatus.Terminated;
                entity.LastModifiedDate = DateTime.Now;

                // Deactivate employee in Employee table
                var emp = await _context.Employees.FirstOrDefaultAsync(e => e.EPFNumber == entity.EpfNumber || (e.Email != null && e.Email.ToLower() == entity.EmployeeEmail.ToLower()));
                if (emp != null)
                {
                    emp.Status = "Terminated";
                }

                await _context.SaveChangesAsync();

                // ── Broadcast Completion Notifications to All Stakeholders ──
                // 1. Notify Employee
                var empIds = await GetEmployeeUserIdentifiersAsync(entity.EmployeeEmail, entity.EpfNumber);
                await SendNotificationsAsync(
                    empIds,
                    "Employment Termination Finalized",
                    $"Your employment termination process has been finalized effective {entity.EffectiveTerminationDate:MMMM dd, yyyy}. All clearances have been processed.",
                    CoreNotificationType.Info,
                    entity.Id,
                    $"/Transfer/Separation?ActiveTab=Terminations"
                );

                // 2. Notify Department Heads
                var deptHeadIds = await GetDepartmentHeadUserIdentifiersAsync(entity.Branch);
                await SendNotificationsAsync(
                    deptHeadIds,
                    "Termination Process Completed ✅",
                    $"Termination request #{entity.Id} for {entity.EmployeeName} ({entity.Branch}) has been fully approved and finalized by HR.",
                    CoreNotificationType.Approved,
                    entity.Id,
                    $"/DepartmentHead/ReviewTermination/{entity.Id}"
                );

                // 3. Notify Branch Manager
                var bmIds = await GetBranchManagerUserIdentifiersAsync(entity.Branch);
                await SendNotificationsAsync(
                    bmIds,
                    "Termination Process Completed ✅",
                    $"Termination request #{entity.Id} for {entity.EmployeeName} has been officially completed and finalized by HR.",
                    CoreNotificationType.Approved,
                    entity.Id,
                    $"/BranchManager/ReviewTermination/{entity.Id}"
                );

                // 4. Notify Area Manager
                var amIds = await GetAreaManagerUserIdentifiersAsync(entity.Branch);
                await SendNotificationsAsync(
                    amIds,
                    "Termination Process Completed ✅",
                    $"Termination request #{entity.Id} for {entity.EmployeeName} has been finalized by HR.",
                    CoreNotificationType.Approved,
                    entity.Id,
                    $"/AreaManager/ReviewTermination/{entity.Id}"
                );

                // 5. Notify HR Officers
                var hrOfficerIds = await GetHROfficerUserIdentifiersAsync(entity.Branch);
                await SendNotificationsAsync(
                    hrOfficerIds,
                    "Termination Finalized by HR ✅",
                    $"Termination request #{entity.Id} for {entity.EmployeeName} ({entity.EpfNumber}) has been finalized and employee record updated.",
                    CoreNotificationType.Approved,
                    entity.Id,
                    $"/Termination/Details?id={entity.Id}"
                );
            }
            else
            {
                entity.Status = TerminationRequestStatus.HRRejected;
                entity.LastModifiedDate = DateTime.Now;
                await _context.SaveChangesAsync();

                var hrOfficerIds = await GetHROfficerUserIdentifiersAsync(entity.Branch);
                await SendNotificationsAsync(
                    hrOfficerIds,
                    "Termination Rejected by HR ❌",
                    $"Termination request #{entity.Id} for {entity.EmployeeName} was rejected at HR finalization stage. Reason: {comments}",
                    CoreNotificationType.Rejected,
                    entity.Id,
                    $"/Termination/Details?id={entity.Id}"
                );
            }

            return (true, null);
        }

        // Backward compatibility
        public async Task<bool> ApproveTerminationAsync(int id, string comments, string approverEmail)
        {
            var res = await FinalizeTerminationAsync(id, true, comments, approverEmail);
            return res.Success;
        }

        public async Task<bool> RejectTerminationAsync(int id, string comments, string approverEmail)
        {
            var res = await FinalizeTerminationAsync(id, false, comments, approverEmail);
            return res.Success;
        }

        public async Task<bool> ProcessFinanceClearanceAsync(int id)
        {
            var res = await FinalizeTerminationAsync(id, true, "Finance clearance completed automatically.", "system@kanrich.lk");
            return res.Success;
        }

        // ── Document Management ──
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

            var request = await _context.TerminationRequests.FindAsync(doc.TerminationRequestId);
            if (request == null || request.Status != TerminationRequestStatus.Draft)
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
                .Include(t => t.DepartmentReviews)
                .Where(t => t.EmployeeEmail == employeeEmail)
                .OrderByDescending(t => t.CreatedDate)
                .ToListAsync();

            return entities.Select(MapToViewModel).ToList();
        }

        // ── Notification Helpers ──
        private async Task<List<string>> GetDepartmentHeadUserIdentifiersAsync(string? branchName)
        {
            var role = await _context.Roles.FirstOrDefaultAsync(r => r.Name == "Department Head");
            if (role == null) return new List<string>();

            var bKey = branchName?.Trim().ToLower().Replace("branch", "").Trim() ?? "";

            var users = await (from ur in _context.UserRoles
                               join u in _context.Users on ur.UserId equals u.Id
                               where ur.RoleId == role.Id
                               select new { u.Id, u.UserName, u.Email, u.Branch, u.Department })
                              .ToListAsync();

            var employees = await _context.Employees
                .Include(e => e.Branch)
                .Include(e => e.Department)
                .Where(e => e.Branch != null)
                .Select(e => new { e.Id, e.Email, e.EPFNumber, BranchName = e.Branch!.Name, DeptName = e.Department != null ? e.Department.Name : "" })
                .ToListAsync();

            var empByEmail = employees.Where(e => !string.IsNullOrEmpty(e.Email)).GroupBy(e => e.Email!.ToLower()).ToDictionary(g => g.Key, g => g.First());

            return users
                .Where(u =>
                {
                    if (IsManagerialDept(u.Department)) return false;

                    var ub = u.Branch?.Trim().ToLower().Replace("branch", "").Trim() ?? "";
                    if (!string.IsNullOrEmpty(ub) && (ub == bKey || ub.Contains(bKey) || bKey.Contains(ub)))
                    {
                        if (!string.IsNullOrEmpty(u.Email) && empByEmail.TryGetValue(u.Email.ToLower(), out var emp1) && IsManagerialDept(emp1.DeptName))
                            return false;
                        return true;
                    }

                    if (!string.IsNullOrEmpty(u.Email) && empByEmail.TryGetValue(u.Email.ToLower(), out var emp))
                    {
                        if (IsManagerialDept(emp.DeptName)) return false;
                        var eb = emp.BranchName.Trim().ToLower().Replace("branch", "").Trim();
                        if (eb == bKey || eb.Contains(bKey) || bKey.Contains(eb)) return true;
                    }

                    return false;
                })
                .Select(u => u.Id)
                .Distinct()
                .ToList();
        }

        private async Task<List<string>> GetBranchManagerUserIdentifiersAsync(string? branchName)
        {
            var role = await _context.Roles.FirstOrDefaultAsync(r => r.Name == "Branch Manager");
            if (role == null) return new List<string>();

            var bKey = branchName?.Trim().ToLower().Replace("branch", "").Trim() ?? "";

            var users = await (from ur in _context.UserRoles
                               join u in _context.Users on ur.UserId equals u.Id
                               where ur.RoleId == role.Id
                               select new { u.Id, u.UserName, u.Email, u.Branch })
                              .ToListAsync();

            var employees = await _context.Employees
                .Include(e => e.Branch)
                .Where(e => e.Branch != null)
                .Select(e => new { e.Email, BranchName = e.Branch!.Name })
                .ToListAsync();

            var empByEmail = employees.Where(e => !string.IsNullOrEmpty(e.Email)).GroupBy(e => e.Email!.ToLower()).ToDictionary(g => g.Key, g => g.First());

            return users
                .Where(u =>
                {
                    var ub = u.Branch?.Trim().ToLower().Replace("branch", "").Trim() ?? "";
                    if (!string.IsNullOrEmpty(ub) && (ub == bKey || ub.Contains(bKey) || bKey.Contains(ub)))
                        return true;

                    if (!string.IsNullOrEmpty(u.Email) && empByEmail.TryGetValue(u.Email.ToLower(), out var emp))
                    {
                        var eb = emp.BranchName.Trim().ToLower().Replace("branch", "").Trim();
                        if (eb == bKey || eb.Contains(bKey) || bKey.Contains(eb)) return true;
                    }

                    return false;
                })
                .Select(u => u.Id)
                .Distinct()
                .ToList();
        }

        private async Task<List<string>> GetAreaManagerUserIdentifiersAsync(string? branchName)
        {
            var role = await _context.Roles.FirstOrDefaultAsync(r => r.Name == "Area Manager");
            if (role == null) return new List<string>();

            int branchId = 0;
            if (!string.IsNullOrWhiteSpace(branchName))
            {
                var b = await _context.Branches.FirstOrDefaultAsync(b => b.Name.ToLower() == branchName.Trim().ToLower() ||
                                                                        b.Name.ToLower().Replace("branch", "").Trim() == branchName.ToLower().Replace("branch", "").Trim());
                if (b != null) branchId = b.Id;
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
                else if (branchId > 0)
                {
                    var managedIds = u.ManagedBranches.Split(',')
                        .Select(s => int.TryParse(s.Trim(), out var id) ? id : 0)
                        .Where(id => id > 0)
                        .ToHashSet();

                    if (managedIds.Contains(branchId)) result.Add(u.Id);
                }
            }

            return result.Distinct().ToList();
        }

        private async Task<List<string>> GetHROfficerUserIdentifiersAsync(string? branchName)
        {
            var role = await _context.Roles.FirstOrDefaultAsync(r => r.Name == "HR Officer");
            var hrManagerRole = await _context.Roles.FirstOrDefaultAsync(r => r.Name == "HR Manager");

            var roleIds = new List<string>();
            if (role != null) roleIds.Add(role.Id);
            if (hrManagerRole != null) roleIds.Add(hrManagerRole.Id);

            if (!roleIds.Any()) return new List<string>();

            int branchId = 0;
            if (!string.IsNullOrWhiteSpace(branchName))
            {
                var b = await _context.Branches.FirstOrDefaultAsync(b => b.Name.ToLower() == branchName.Trim().ToLower() ||
                                                                        b.Name.ToLower().Replace("branch", "").Trim() == branchName.ToLower().Replace("branch", "").Trim());
                if (b != null) branchId = b.Id;
            }

            var bKey = branchName?.Trim().ToLower().Replace("branch", "").Trim() ?? "";

            var users = await (from ur in _context.UserRoles
                               join u in _context.Users on ur.UserId equals u.Id
                               where roleIds.Contains(ur.RoleId)
                               select new { u.Id, u.UserName, u.Branch, u.ManagedBranches })
                              .ToListAsync();

            var result = new List<string>();
            foreach (var u in users)
            {
                if (string.IsNullOrWhiteSpace(u.ManagedBranches))
                {
                    var ub = u.Branch?.Trim().ToLower().Replace("branch", "").Trim() ?? "";
                    if (string.IsNullOrWhiteSpace(ub) || (!string.IsNullOrEmpty(bKey) && (ub == bKey || ub.Contains(bKey) || bKey.Contains(ub))))
                    {
                        result.Add(u.Id);
                    }
                }
                else if (branchId > 0)
                {
                    var managedIds = u.ManagedBranches.Split(',')
                        .Select(s => int.TryParse(s.Trim(), out var id) ? id : 0)
                        .Where(id => id > 0)
                        .ToHashSet();

                    if (managedIds.Contains(branchId)) result.Add(u.Id);
                }
            }

            return result.Distinct().ToList();
        }

        private async Task<List<string>> GetEmployeeUserIdentifiersAsync(string? email, string? epf)
        {
            var eKey = email?.Trim().ToLower() ?? "";
            var epfKey = epf?.Trim().ToLower() ?? "";

            var userIds = await _context.Users
                .Where(u => (!string.IsNullOrEmpty(eKey) && ((u.Email != null && u.Email.ToLower() == eKey) || (u.UserName != null && u.UserName.ToLower() == eKey))) ||
                            (!string.IsNullOrEmpty(epfKey) && u.EpfNumber != null && u.EpfNumber.ToLower() == epfKey))
                .Select(u => u.Id)
                .Distinct()
                .ToListAsync();

            if (!userIds.Any() && !string.IsNullOrWhiteSpace(email))
            {
                userIds.Add(email.Trim());
            }

            return userIds.Distinct().ToList();
        }

        private async Task SendNotificationsAsync(
            IEnumerable<string> recipientIdentifiers,
            string title,
            string message,
            CoreNotificationType type,
            int terminationRequestId,
            string targetUrl = "")
        {
            var distinctRecipients = recipientIdentifiers
                .Where(r => !string.IsNullOrWhiteSpace(r))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var recipient in distinctRecipients)
            {
                try
                {
                    await _notificationService.CreateNotificationAsync(recipient, title, message, type, terminationRequestId, targetUrl);
                }
                catch
                {
                    // Prevent individual notification failure from stopping the workflow
                }
            }
        }

        // ── Mapper ──
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
                BMReview = entity.BMReview,
                BMReviewDate = entity.BMReviewDate,
                BMComments = entity.BMComments,
                BMEmail = entity.BMEmail,
                AMReview = entity.AMReview,
                AMReviewDate = entity.AMReviewDate,
                AMComments = entity.AMComments,
                AMEmail = entity.AMEmail,
                HRReview = entity.HRReview,
                HRReviewDate = entity.HRReviewDate,
                HRComments = entity.HRComments,
                HREmail = entity.HREmail,
                FinanceClearanceCompleted = entity.FinanceClearanceCompleted,
                FinanceClearanceDate = entity.FinanceClearanceDate,
                FinanceClearanceNotes = entity.FinanceClearanceNotes,
                DocumentCount = entity.Documents?.Count ?? 0,
                Documents = entity.Documents?.Select(d => new TerminationDocumentViewModel
                {
                    Id = d.Id,
                    FileName = d.FileName,
                    ContentType = d.ContentType,
                    DocumentType = d.DocumentType.ToString(),
                    UploadedDate = d.UploadedDate
                }).ToList() ?? new List<TerminationDocumentViewModel>(),
                DepartmentReviews = entity.DepartmentReviews?.Select(dr => new TerminationDepartmentReviewViewModel
                {
                    Id = dr.Id,
                    TerminationRequestId = dr.TerminationRequestId,
                    DepartmentName = dr.DepartmentName,
                    ReviewerUserId = dr.ReviewerUserId,
                    ReviewerName = dr.ReviewerName,
                    ReviewerEmail = dr.ReviewerEmail,
                    Status = dr.Status,
                    Comments = dr.Comments,
                    ReviewDate = dr.ReviewDate
                }).ToList() ?? new List<TerminationDepartmentReviewViewModel>()
            };
        }
    }
}
