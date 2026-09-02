using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HRMS.Domain.Entities.Resignation;
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
        Task<(bool Success, string? Error)> UpdateDraftAsync(int id, DateTime effectiveDate, string? reasonForResignation, string? additionalRemarks, bool hasOutstandingLoans, bool isLoanGuarantor);
        Task<(bool Success, string? Error)> DeleteDraftAsync(int id, string userEmail);
        Task<ResignationRequestViewModel?> GetByIdAsync(int id);
        Task<List<ResignationRequestViewModel>> GetMyResignationsAsync(string employeeEmail);
        
        // ── Stage 2: Department Head (in branch) ──
        Task<List<ResignationRequestViewModel>> GetPendingForDeptHeadAsync(string branchName, string departmentName);
        Task<List<ResignationRequestViewModel>> GetReviewedByDeptHeadAsync(string branchName, string departmentName);
        Task<bool> DeptHeadReviewAsync(int id, string departmentName, bool approved, string comments, string reviewerEmail, string reviewerName, string reviewerUserId);

        // ── Stage 3: Branch Manager (in branch) ──
        Task<List<ResignationRequestViewModel>> GetPendingForBranchManagerAsync(string branchName);
        Task<List<ResignationRequestViewModel>> GetReviewedByBranchManagerAsync(string branchName);
        Task<bool> BranchManagerApproveAsync(int id, string comments, string reviewerEmail);
        Task<bool> BranchManagerRejectAsync(int id, string comments, string reviewerEmail);

        // ── Stage 4: Area Manager (managed branches) ──
        Task<List<ResignationRequestViewModel>> GetPendingForAreaManagerAsync(List<int>? managedBranchIds = null, string? areaManagerBranch = null);
        Task<List<ResignationRequestViewModel>> GetReviewedByAreaManagerAsync(List<int>? managedBranchIds = null, string? areaManagerBranch = null);
        Task<bool> AreaManagerApproveAsync(int id, string comments, string reviewerEmail);
        Task<bool> AreaManagerRejectAsync(int id, string comments, string reviewerEmail);

        // ── Stage 5: HR Officer / HR Manager (managed branches or global) ──
        Task<List<ResignationRequestViewModel>> GetPendingForHRManagerAsync(List<int>? managedBranchIds = null);
        Task<List<ResignationRequestViewModel>> GetReviewedByHRManagerAsync(List<int>? managedBranchIds = null);
        Task<bool> HRManagerApproveAsync(int id, string comments, string reviewerEmail);
        Task<bool> HRManagerRejectAsync(int id, string comments, string reviewerEmail);

        Task<List<ResignationRequestViewModel>> GetAllAsync(string? statusFilter = null, string? search = null);
        Task<(bool Success, string? Error)> ProcessEffectiveDateAsync(int id, string processedBy, UserManager<ApplicationUser> userManager);
        Task<(bool Success, string? Error)> ReactivateAccountAsync(int id, string reactivatedBy, UserManager<ApplicationUser> userManager);
        Task<int> AddDocumentAsync(int resignationRequestId, string fileName, string contentType, byte[] data);
        Task<(byte[]? Data, string? FileName, string? ContentType)> GetDocumentAsync(int documentId);
    }

    /// <summary>
    /// Service responsible for managing employee resignations.
    /// Workflow: Employee -> All Department Heads in Branch -> Branch Manager -> Area Manager -> Assigned HR Officer
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

        // ── Stage 1: Create Draft ─────────────────────────────────────────────
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

        public async Task<(bool Success, string? Error)> UpdateDraftAsync(
            int id,
            DateTime effectiveDate,
            string? reasonForResignation,
            string? additionalRemarks,
            bool hasOutstandingLoans,
            bool isLoanGuarantor)
        {
            var entity = await _context.ResignationRequests.FirstOrDefaultAsync(r => r.Id == id);
            if (entity == null)
                return (false, "Resignation request not found.");

            if (entity.Status != ResignationStatus.Draft)
                return (false, "Only draft requests can be edited.");

            entity.EffectiveDate = effectiveDate;
            if (effectiveDate != default)
            {
                var refDate = entity.ResignationDate != default ? entity.ResignationDate.Date : DateTime.Today;
                entity.NoticePeriodDays = Math.Max(0, (effectiveDate.Date - refDate).Days);
            }
            entity.ReasonForResignation = reasonForResignation ?? string.Empty;
            entity.AdditionalRemarks = additionalRemarks;
            entity.HasOutstandingLoans = hasOutstandingLoans;
            entity.IsLoanGuarantor = isLoanGuarantor;
            entity.LastModifiedDate = DateTime.Now;

            await _context.SaveChangesAsync();
            return (true, null);
        }

        public async Task<(bool Success, string? Error)> DeleteDraftAsync(int id, string userEmail)
        {
            var entity = await _context.ResignationRequests
                .Include(r => r.Documents)
                .Include(r => r.DepartmentReviews)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (entity == null)
                return (false, "Resignation draft not found.");

            if (entity.Status != ResignationStatus.Draft)
                return (false, "Only draft resignation requests can be deleted.");

            if (!string.IsNullOrWhiteSpace(userEmail))
            {
                var normUser = userEmail.Trim().ToLower();
                var normOwner = (entity.EmployeeEmail ?? "").Trim().ToLower();
                var normInit = (entity.InitiatedBy ?? "").Trim().ToLower();
                if (normUser != normOwner && normUser != normInit && !normOwner.Contains(normUser) && !normUser.Contains(normOwner))
                {
                    return (false, "You are not authorized to delete this draft.");
                }
            }

            if (entity.DepartmentReviews.Any())
            {
                _context.ResignationDepartmentReviews.RemoveRange(entity.DepartmentReviews);
            }
            if (entity.Documents.Any())
            {
                _context.ResignationDocuments.RemoveRange(entity.Documents);
            }

            _context.ResignationRequests.Remove(entity);
            await _context.SaveChangesAsync();
            return (true, null);
        }

        // ── Stage 1: Validate & Submit ────────────────────────────────────────
        public async Task<(bool Success, string? Error)> ValidateAndSubmitAsync(int id)
        {
            var entity = await _context.ResignationRequests
                .Include(r => r.Documents)
                .Include(r => r.DepartmentReviews)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (entity == null)
                return (false, "Resignation request not found.");

            if (entity.Status != ResignationStatus.Draft)
                return (false, "Only draft requests can be submitted.");

            if (!entity.Documents.Any())
                return (false, "At least one supporting document must be attached before submission.");

            var minDate = entity.ResignationDate.Date.AddMonths(1);
            if (entity.EffectiveDate.Date < minDate)
                return (false, "Last working day must be at least 1 month from the requesting date.");

            // Initialize department reviews for non-managerial department heads in the employee's branch
            await InitializeDepartmentReviewsAsync(entity);

            if (entity.DepartmentReviews.Any())
            {
                entity.Status = ResignationStatus.SubmittedForApproval;
                entity.LastModifiedDate = DateTime.Now;
                await _context.SaveChangesAsync();

                // 1. Notify all Department Heads in this branch
                var deptHeadIds = await GetDepartmentHeadUserIdentifiersAsync(entity.Branch);
                await SendNotificationsAsync(
                    deptHeadIds,
                    "New Resignation Request Pending Review",
                    $"Resignation request #{entity.Id} for {entity.EmployeeName} ({entity.Branch}) is pending your department's review.",
                    CoreNotificationType.Info,
                    entity.Id,
                    $"/DepartmentHead/ReviewResignation/{entity.Id}"
                );

                // 2. Notify HR Officers assigned to this branch
                var hrOfficerIds = await GetHROfficerUserIdentifiersAsync(entity.Branch);
                await SendNotificationsAsync(
                    hrOfficerIds,
                    "Resignation Request Submitted",
                    $"Resignation request #{entity.Id} for {entity.EmployeeName} in {entity.Branch} has been submitted.",
                    CoreNotificationType.Info,
                    entity.Id,
                    $"/HRManager/ReviewResignation/{entity.Id}"
                );

                // 3. Notify Employee
                var empIds = await GetEmployeeUserIdentifiersAsync(entity.EmployeeEmail, entity.EpfNumber);
                await SendNotificationsAsync(
                    empIds,
                    "Resignation Request Submitted",
                    $"Your resignation request #{entity.Id} has been submitted and is pending review by Department Heads in your branch.",
                    CoreNotificationType.Info,
                    entity.Id,
                    $"/Resignation/Details/{entity.Id}"
                );
            }
            else
            {
                // If branch has no non-managerial department reviews required, advance directly to Branch Manager
                entity.Status = ResignationStatus.DeptHeadsApproved;
                entity.LastModifiedDate = DateTime.Now;
                await _context.SaveChangesAsync();

                var bmIds = await GetBranchManagerUserIdentifiersAsync(entity.Branch);
                await SendNotificationsAsync(
                    bmIds,
                    "Resignation Request Awaiting Your Review",
                    $"Resignation request #{entity.Id} for {entity.EmployeeName} ({entity.Branch}) has been submitted and awaits your review.",
                    CoreNotificationType.Info,
                    entity.Id,
                    $"/BranchManager/ReviewResignation/{entity.Id}"
                );

                var empIds = await GetEmployeeUserIdentifiersAsync(entity.EmployeeEmail, entity.EpfNumber);
                await SendNotificationsAsync(
                    empIds,
                    "Resignation Request Submitted",
                    $"Your resignation request #{entity.Id} has been submitted and forwarded to the Branch Manager.",
                    CoreNotificationType.Info,
                    entity.Id,
                    $"/Resignation/Details/{entity.Id}"
                );
            }

            return (true, null);
        }

        private async Task InitializeDepartmentReviewsAsync(ResignationRequest entity)
        {
            if (string.IsNullOrWhiteSpace(entity.Branch)) return;

            var branchName = entity.Branch.Trim().ToLower();
            var branch = await _context.Branches.FirstOrDefaultAsync(b => b.Name.ToLower() == branchName);

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
                    (!string.IsNullOrEmpty(x.uBranch) && x.uBranch.Trim().ToLower() == branchName) ||
                    (!string.IsNullOrEmpty(x.empBranch) && x.empBranch.Trim().ToLower() == branchName));

                foreach (var dh in matchingDHs)
                {
                    var dName = !string.IsNullOrWhiteSpace(dh.uDept) ? dh.uDept : dh.empDept;
                    if (!string.IsNullOrWhiteSpace(dName) && !IsManagerialDept(dName))
                        deptNames.Add(dName.Trim());
                }
            }

            // Fallback: If no departments were found, add the employee's own department (if not managerial)
            if (!deptNames.Any() && !string.IsNullOrWhiteSpace(entity.Department) && !IsManagerialDept(entity.Department))
            {
                deptNames.Add(entity.Department.Trim());
            }

            // Explicitly remove any managerial departments
            deptNames.RemoveWhere(IsManagerialDept);

            // Clear any existing and add fresh department review records
            var existingReviews = await _context.ResignationDepartmentReviews
                .Where(r => r.ResignationRequestId == entity.Id)
                .ToListAsync();
            if (existingReviews.Any())
            {
                _context.ResignationDepartmentReviews.RemoveRange(existingReviews);
            }

            foreach (var dept in deptNames)
            {
                _context.ResignationDepartmentReviews.Add(new ResignationDepartmentReview
                {
                    ResignationRequestId = entity.Id,
                    DepartmentName = dept,
                    Status = "Pending"
                });
            }
        }

        private static bool IsManagerialDept(string? name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            var norm = name.Trim().ToLower();
            return norm == "managerial" || norm == "management" || norm.StartsWith("managerial") || norm.StartsWith("management");
        }

        private static bool MatchBranch(string? a, string? b)
        {
            if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b)) return false;
            var normA = a.Trim().ToLower().Replace("branch", "").Trim();
            var normB = b.Trim().ToLower().Replace("branch", "").Trim();
            return normA == normB || normA.Contains(normB) || normB.Contains(normA);
        }

        private static bool MatchDept(string? a, string? b)
        {
            if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b)) return false;
            var normA = a.Trim().ToLower().Replace("department", "").Replace("dept", "").Trim();
            var normB = b.Trim().ToLower().Replace("department", "").Replace("dept", "").Trim();
            return normA == normB || normA.Contains(normB) || normB.Contains(normA);
        }

        // ── Stage 2: Department Head Review ──────────────────────────────────
        public async Task<List<ResignationRequestViewModel>> GetPendingForDeptHeadAsync(string branchName, string departmentName)
        {
            if (!string.IsNullOrWhiteSpace(departmentName) && IsManagerialDept(departmentName))
                return new List<ResignationRequestViewModel>();

            var entities = await _context.ResignationRequests
                .AsSplitQuery()
                .Include(r => r.Documents)
                .Include(r => r.DepartmentReviews)
                .Where(r => r.Status == ResignationStatus.SubmittedForApproval)
                .OrderByDescending(r => r.ResignationDate)
                .ToListAsync();

            // Filter to branch
            if (!string.IsNullOrWhiteSpace(branchName))
            {
                entities = entities.Where(r => MatchBranch(r.Branch, branchName)).ToList();
            }

            // Auto-repair / Lazy-initialize any resignation request that does not have department reviews yet
            bool anyRepaired = false;
            foreach (var r in entities)
            {
                var managerialReviews = r.DepartmentReviews.Where(dr => IsManagerialDept(dr.DepartmentName)).ToList();
                if (managerialReviews.Any())
                {
                    _context.ResignationDepartmentReviews.RemoveRange(managerialReviews);
                    foreach (var mr in managerialReviews) r.DepartmentReviews.Remove(mr);
                    anyRepaired = true;
                }

                if (!r.DepartmentReviews.Any())
                {
                    await InitializeDepartmentReviewsAsync(r);
                    anyRepaired = true;
                }
            }
            if (anyRepaired)
            {
                await _context.SaveChangesAsync();
            }

            // Filter where this specific department's review is Pending
            var pendingForThisDept = entities.Where(r =>
            {
                if (string.IsNullOrWhiteSpace(departmentName))
                    return r.DepartmentReviews.Any(dr => dr.Status == "Pending" && !IsManagerialDept(dr.DepartmentName)) || !r.DepartmentReviews.Any();

                var deptReview = r.DepartmentReviews.FirstOrDefault(dr => MatchDept(dr.DepartmentName, departmentName));
                if (deptReview != null)
                {
                    return deptReview.Status == "Pending";
                }

                // If no review record specifically matching this department name yet, it is still pending for this DH
                return true;
            }).ToList();

            return pendingForThisDept.Select(Map).ToList();
        }

        public async Task<List<ResignationRequestViewModel>> GetReviewedByDeptHeadAsync(string branchName, string departmentName)
        {
            if (!string.IsNullOrWhiteSpace(departmentName) && IsManagerialDept(departmentName))
                return new List<ResignationRequestViewModel>();

            var entities = await _context.ResignationRequests
                .AsSplitQuery()
                .Include(r => r.Documents)
                .Include(r => r.DepartmentReviews)
                .OrderByDescending(r => r.LastModifiedDate)
                .ToListAsync();

            if (!string.IsNullOrWhiteSpace(branchName))
            {
                entities = entities.Where(r => MatchBranch(r.Branch, branchName)).ToList();
            }

            var reviewedForThisDept = entities.Where(r =>
            {
                if (string.IsNullOrWhiteSpace(departmentName))
                    return r.DepartmentReviews.Any(dr => dr.Status != "Pending" && !IsManagerialDept(dr.DepartmentName));

                var deptReview = r.DepartmentReviews.FirstOrDefault(dr => MatchDept(dr.DepartmentName, departmentName));
                return deptReview != null && deptReview.Status != "Pending";
            }).ToList();

            return reviewedForThisDept.Select(Map).ToList();
        }

        public async Task<bool> DeptHeadReviewAsync(
            int id,
            string departmentName,
            bool approved,
            string comments,
            string reviewerEmail,
            string reviewerName,
            string reviewerUserId)
        {
            if (IsManagerialDept(departmentName))
                return false;

            var entity = await _context.ResignationRequests
                .Include(r => r.DepartmentReviews)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (entity == null || entity.Status != ResignationStatus.SubmittedForApproval)
                return false;

            if (!entity.DepartmentReviews.Any())
            {
                await InitializeDepartmentReviewsAsync(entity);
            }

            var deptReview = entity.DepartmentReviews
                .FirstOrDefault(dr => MatchDept(dr.DepartmentName, departmentName));

            if (deptReview == null)
            {
                deptReview = new ResignationDepartmentReview
                {
                    ResignationRequestId = entity.Id,
                    DepartmentName = departmentName.Trim(),
                };
                entity.DepartmentReviews.Add(deptReview);
            }

            deptReview.ReviewerUserId = reviewerUserId;
            deptReview.ReviewerName   = reviewerName;
            deptReview.ReviewerEmail  = reviewerEmail;
            deptReview.Comments       = comments;
            deptReview.ReviewDate     = DateTime.Now;
            deptReview.Status         = approved ? "Approved" : "Rejected";

            entity.LastModifiedDate   = DateTime.Now;

            if (!approved)
            {
                // Rejection by any Department Head rejects the resignation request
                entity.Status = ResignationStatus.DeptHeadRejected;
                await _context.SaveChangesAsync();

                // Notifications
                var empIds = await GetEmployeeUserIdentifiersAsync(entity.EmployeeEmail, entity.EpfNumber);
                await SendNotificationsAsync(
                    empIds,
                    $"Resignation Rejected by {departmentName} Department Head ❌",
                    $"Your resignation request #{entity.Id} was rejected by the {departmentName} Department Head. Reason: {comments}",
                    CoreNotificationType.Rejected,
                    entity.Id,
                    $"/Resignation/Details/{entity.Id}"
                );

                var hrOfficerIds = await GetHROfficerUserIdentifiersAsync(entity.Branch);
                await SendNotificationsAsync(
                    hrOfficerIds,
                    $"Resignation #{entity.Id} Rejected by {departmentName} Dept Head",
                    $"Resignation request #{entity.Id} for {entity.EmployeeName} ({entity.Branch}) was rejected by {departmentName} Dept Head. Reason: {comments}",
                    CoreNotificationType.Rejected,
                    entity.Id,
                    $"/HRManager/ReviewResignation/{entity.Id}"
                );
            }
            else
            {
                // Check if ALL department reviews for this request are now Approved
                bool allApproved = entity.DepartmentReviews.Any() &&
                                   entity.DepartmentReviews.All(dr => dr.Status == "Approved");

                if (allApproved)
                {
                    // Escalate to Branch Manager
                    entity.Status = ResignationStatus.DeptHeadsApproved;
                    await _context.SaveChangesAsync();

                    // 1. Notify Branch Manager of this branch
                    var bmIds = await GetBranchManagerUserIdentifiersAsync(entity.Branch);
                    await SendNotificationsAsync(
                        bmIds,
                        "Resignation Request Awaiting Your Review",
                        $"Resignation request #{entity.Id} for {entity.EmployeeName} ({entity.Branch}) has been approved by all Department Heads and awaits your review.",
                        CoreNotificationType.Info,
                        entity.Id,
                        $"/BranchManager/ReviewResignation/{entity.Id}"
                    );

                    // 2. Notify Employee
                    var empIds = await GetEmployeeUserIdentifiersAsync(entity.EmployeeEmail, entity.EpfNumber);
                    await SendNotificationsAsync(
                        empIds,
                        "Resignation Approved by All Department Heads ✅",
                        $"Your resignation request #{entity.Id} has been approved by all Department Heads in your branch and forwarded to the Branch Manager.",
                        CoreNotificationType.Approved,
                        entity.Id,
                        $"/Resignation/Details/{entity.Id}"
                    );

                    // 3. Notify HR Officers
                    var hrOfficerIds = await GetHROfficerUserIdentifiersAsync(entity.Branch);
                    await SendNotificationsAsync(
                        hrOfficerIds,
                        "Resignation Approved by All Dept Heads ✅",
                        $"Resignation request #{entity.Id} for {entity.EmployeeName} ({entity.Branch}) has been approved by all Department Heads and forwarded to Branch Manager.",
                        CoreNotificationType.Approved,
                        entity.Id,
                        $"/HRManager/ReviewResignation/{entity.Id}"
                    );
                }
                else
                {
                    await _context.SaveChangesAsync();

                    // Notify Employee of intermediate approval
                    int approvedCount = entity.DepartmentReviews.Count(dr => dr.Status == "Approved");
                    int totalCount = entity.DepartmentReviews.Count;

                    var empIds = await GetEmployeeUserIdentifiersAsync(entity.EmployeeEmail, entity.EpfNumber);
                    await SendNotificationsAsync(
                        empIds,
                        $"Resignation Approved by {departmentName} Dept Head",
                        $"Your resignation request #{entity.Id} was approved by {departmentName} Department Head ({approvedCount}/{totalCount} department approvals completed).",
                        CoreNotificationType.Info,
                        entity.Id,
                        $"/Resignation/Details/{entity.Id}"
                    );
                }
            }

            return true;
        }

        // ── Stage 3: Branch Manager Review ────────────────────────────────────
        public async Task<List<ResignationRequestViewModel>> GetPendingForBranchManagerAsync(string branchName)
        {
            if (string.IsNullOrWhiteSpace(branchName)) return new List<ResignationRequestViewModel>();

            var bKey = branchName.Trim().ToLower();

            var list = await _context.ResignationRequests
                .AsSplitQuery()
                .Include(r => r.Documents)
                .Include(r => r.DepartmentReviews)
                .Where(r => r.Status == ResignationStatus.DeptHeadsApproved
                         && r.Branch != null && r.Branch.ToLower() == bKey)
                .OrderByDescending(r => r.LastModifiedDate)
                .ToListAsync();

            return list.Select(Map).ToList();
        }

        public async Task<List<ResignationRequestViewModel>> GetReviewedByBranchManagerAsync(string branchName)
        {
            if (string.IsNullOrWhiteSpace(branchName)) return new List<ResignationRequestViewModel>();

            var bKey = branchName.Trim().ToLower();

            var list = await _context.ResignationRequests
                .AsSplitQuery()
                .Include(r => r.Documents)
                .Include(r => r.DepartmentReviews)
                .Where(r => r.BMReview != null
                         && r.Branch != null && r.Branch.ToLower() == bKey)
                .OrderByDescending(r => r.BMReviewDate)
                .ToListAsync();

            return list.Select(Map).ToList();
        }

        public async Task<bool> BranchManagerApproveAsync(int id, string comments, string reviewerEmail)
        {
            var entity = await _context.ResignationRequests
                .Include(r => r.DepartmentReviews)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (entity == null || entity.Status != ResignationStatus.DeptHeadsApproved)
                return false;

            entity.BMReview     = "Approved";
            entity.BMReviewDate = DateTime.Now;
            entity.BMComments   = comments;
            entity.BMEmail      = reviewerEmail;
            entity.Status       = ResignationStatus.BMApproved;
            entity.LastModifiedDate = DateTime.Now;

            await _context.SaveChangesAsync();

            // 1. Notify Area Manager(s) for this branch
            var areaManagerIds = await GetAreaManagerUserIdentifiersAsync(entity.Branch);
            await SendNotificationsAsync(
                areaManagerIds,
                "Resignation Request Awaiting Area Manager Approval",
                $"Resignation request #{entity.Id} for {entity.EmployeeName} ({entity.Branch}) has been acknowledged by the Branch Manager and awaits your approval.",
                CoreNotificationType.Info,
                entity.Id,
                $"/AreaManager/ReviewResignation/{entity.Id}"
            );

            // 2. Notify Department Heads in this branch
            var deptHeadIds = await GetDepartmentHeadUserIdentifiersAsync(entity.Branch);
            await SendNotificationsAsync(
                deptHeadIds,
                "Resignation Acknowledged by Branch Manager ✅",
                $"Resignation request #{entity.Id} for {entity.EmployeeName} was acknowledged by the Branch Manager and forwarded to Area Manager.",
                CoreNotificationType.Approved,
                entity.Id,
                $"/DepartmentHead/ReviewResignation/{entity.Id}"
            );

            // 3. Notify HR Officers
            var hrOfficerIds = await GetHROfficerUserIdentifiersAsync(entity.Branch);
            await SendNotificationsAsync(
                hrOfficerIds,
                "Resignation Acknowledged by Branch Manager ✅",
                $"Resignation request #{entity.Id} for {entity.EmployeeName} has been acknowledged by Branch Manager and forwarded to Area Manager.",
                CoreNotificationType.Approved,
                entity.Id,
                $"/HRManager/ReviewResignation/{entity.Id}"
            );

            // 4. Notify Employee
            var empIds = await GetEmployeeUserIdentifiersAsync(entity.EmployeeEmail, entity.EpfNumber);
            await SendNotificationsAsync(
                empIds,
                "Resignation Acknowledged by Branch Manager ✅",
                $"Your resignation request #{entity.Id} has been acknowledged by the Branch Manager and forwarded to the Area Manager.",
                CoreNotificationType.Approved,
                entity.Id,
                $"/Resignation/Details/{entity.Id}"
            );

            return true;
        }

        public async Task<bool> BranchManagerRejectAsync(int id, string comments, string reviewerEmail)
        {
            var entity = await _context.ResignationRequests
                .Include(r => r.DepartmentReviews)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (entity == null || entity.Status != ResignationStatus.DeptHeadsApproved)
                return false;

            entity.BMReview     = "Rejected";
            entity.BMReviewDate = DateTime.Now;
            entity.BMComments   = comments;
            entity.BMEmail      = reviewerEmail;
            entity.Status       = ResignationStatus.BMRejected;
            entity.LastModifiedDate = DateTime.Now;

            await _context.SaveChangesAsync();

            // Notify Employee
            var empIds = await GetEmployeeUserIdentifiersAsync(entity.EmployeeEmail, entity.EpfNumber);
            await SendNotificationsAsync(
                empIds,
                "Resignation Rejected by Branch Manager ❌",
                $"Your resignation request #{entity.Id} was rejected by your Branch Manager. Reason: {comments}",
                CoreNotificationType.Rejected,
                entity.Id,
                $"/Resignation/Details/{entity.Id}"
            );

            // Notify Department Heads
            var deptHeadIds = await GetDepartmentHeadUserIdentifiersAsync(entity.Branch);
            await SendNotificationsAsync(
                deptHeadIds,
                "Resignation Rejected by Branch Manager ❌",
                $"Resignation request #{entity.Id} for {entity.EmployeeName} was rejected by Branch Manager. Reason: {comments}",
                CoreNotificationType.Rejected,
                entity.Id,
                $"/DepartmentHead/ReviewResignation/{entity.Id}"
            );

            // Notify HR Officers
            var hrOfficerIds = await GetHROfficerUserIdentifiersAsync(entity.Branch);
            await SendNotificationsAsync(
                hrOfficerIds,
                "Resignation Rejected by Branch Manager ❌",
                $"Resignation request #{entity.Id} for {entity.EmployeeName} was rejected by Branch Manager. Reason: {comments}",
                CoreNotificationType.Rejected,
                entity.Id,
                $"/HRManager/ReviewResignation/{entity.Id}"
            );

            return true;
        }

        // ── Stage 4: Area Manager Review ──────────────────────────────────────
        public async Task<List<ResignationRequestViewModel>> GetPendingForAreaManagerAsync(
            List<int>? managedBranchIds = null, string? areaManagerBranch = null)
        {
            var query = _context.ResignationRequests
                .AsSplitQuery()
                .Include(r => r.Documents)
                .Include(r => r.DepartmentReviews)
                .Where(r => r.Status == ResignationStatus.BMApproved);

            if (managedBranchIds != null && managedBranchIds.Any())
            {
                var branchNames = await _context.Branches
                    .Where(b => managedBranchIds.Contains(b.Id))
                    .Select(b => b.Name.ToLower())
                    .ToListAsync();

                query = query.Where(r => r.Branch != null && branchNames.Contains(r.Branch.ToLower()));
            }
            else if (!string.IsNullOrWhiteSpace(areaManagerBranch))
            {
                var bKey = areaManagerBranch.Trim().ToLower();
                query = query.Where(r => r.Branch != null && r.Branch.ToLower() == bKey);
            }

            var list = await query.OrderByDescending(r => r.BMReviewDate).ToListAsync();
            return list.Select(Map).ToList();
        }

        public async Task<List<ResignationRequestViewModel>> GetReviewedByAreaManagerAsync(
            List<int>? managedBranchIds = null, string? areaManagerBranch = null)
        {
            var query = _context.ResignationRequests
                .AsSplitQuery()
                .Include(r => r.Documents)
                .Include(r => r.DepartmentReviews)
                .Where(r => r.AMReview != null);

            if (managedBranchIds != null && managedBranchIds.Any())
            {
                var branchNames = await _context.Branches
                    .Where(b => managedBranchIds.Contains(b.Id))
                    .Select(b => b.Name.ToLower())
                    .ToListAsync();

                query = query.Where(r => r.Branch != null && branchNames.Contains(r.Branch.ToLower()));
            }
            else if (!string.IsNullOrWhiteSpace(areaManagerBranch))
            {
                var bKey = areaManagerBranch.Trim().ToLower();
                query = query.Where(r => r.Branch != null && r.Branch.ToLower() == bKey);
            }

            var list = await query.OrderByDescending(r => r.AMReviewDate).ToListAsync();
            return list.Select(Map).ToList();
        }

        public async Task<bool> AreaManagerApproveAsync(int id, string comments, string reviewerEmail)
        {
            var entity = await _context.ResignationRequests
                .Include(r => r.DepartmentReviews)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (entity == null || entity.Status != ResignationStatus.BMApproved)
                return false;

            entity.AMReview     = "Approved";
            entity.AMReviewDate = DateTime.Now;
            entity.AMComments   = comments;
            entity.AMEmail      = reviewerEmail;
            entity.Status       = ResignationStatus.AMApproved;
            entity.LastModifiedDate = DateTime.Now;

            await _context.SaveChangesAsync();

            // 1. Notify HR Officers assigned to this branch & Corporate HR Managers
            var hrRecipients = (await GetHRManagerUserIdentifiersAsync())
                .Concat(await GetHROfficerUserIdentifiersAsync(entity.Branch));
            await SendNotificationsAsync(
                hrRecipients,
                "Resignation Request Ready for HR Finalization",
                $"Resignation request #{entity.Id} for {entity.EmployeeName} ({entity.Branch}) has been approved by the Area Manager and is ready for HR finalization.",
                CoreNotificationType.Info,
                entity.Id,
                $"/HRManager/ReviewResignation/{entity.Id}"
            );

            // 2. Notify Department Heads
            var deptHeadIds = await GetDepartmentHeadUserIdentifiersAsync(entity.Branch);
            await SendNotificationsAsync(
                deptHeadIds,
                "Resignation Approved by Area Manager ✅",
                $"Resignation request #{entity.Id} for {entity.EmployeeName} has been approved by Area Manager and forwarded to HR.",
                CoreNotificationType.Approved,
                entity.Id,
                $"/DepartmentHead/ReviewResignation/{entity.Id}"
            );

            // 3. Notify Branch Manager
            var bmIds = await GetBranchManagerUserIdentifiersAsync(entity.Branch);
            await SendNotificationsAsync(
                bmIds,
                "Resignation Approved by Area Manager ✅",
                $"Resignation request #{entity.Id} for {entity.EmployeeName} has been approved by Area Manager and is now awaiting HR finalization.",
                CoreNotificationType.Approved,
                entity.Id,
                $"/BranchManager/ReviewResignation/{entity.Id}"
            );

            // 4. Notify Employee
            var empIds = await GetEmployeeUserIdentifiersAsync(entity.EmployeeEmail, entity.EpfNumber);
            await SendNotificationsAsync(
                empIds,
                "Resignation Approved by Area Manager ✅",
                $"Your resignation request #{entity.Id} has been approved by the Area Manager and is now awaiting final processing by HR.",
                CoreNotificationType.Approved,
                entity.Id,
                $"/Resignation/Details/{entity.Id}"
            );

            return true;
        }

        public async Task<bool> AreaManagerRejectAsync(int id, string comments, string reviewerEmail)
        {
            var entity = await _context.ResignationRequests
                .Include(r => r.DepartmentReviews)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (entity == null || entity.Status != ResignationStatus.BMApproved)
                return false;

            entity.AMReview     = "Rejected";
            entity.AMReviewDate = DateTime.Now;
            entity.AMComments   = comments;
            entity.AMEmail      = reviewerEmail;
            entity.Status       = ResignationStatus.AMRejected;
            entity.LastModifiedDate = DateTime.Now;

            await _context.SaveChangesAsync();

            // Notify Employee, Department Heads, Branch Manager, HR Officers
            var empIds = await GetEmployeeUserIdentifiersAsync(entity.EmployeeEmail, entity.EpfNumber);
            await SendNotificationsAsync(
                empIds,
                "Resignation Rejected by Area Manager ❌",
                $"Your resignation request #{entity.Id} was rejected by the Area Manager. Reason: {comments}",
                CoreNotificationType.Rejected,
                entity.Id,
                $"/Resignation/Details/{entity.Id}"
            );

            var deptHeadIds = await GetDepartmentHeadUserIdentifiersAsync(entity.Branch);
            await SendNotificationsAsync(
                deptHeadIds,
                "Resignation Rejected by Area Manager ❌",
                $"Resignation request #{entity.Id} for {entity.EmployeeName} was rejected by Area Manager. Reason: {comments}",
                CoreNotificationType.Rejected,
                entity.Id,
                $"/DepartmentHead/ReviewResignation/{entity.Id}"
            );

            var bmIds = await GetBranchManagerUserIdentifiersAsync(entity.Branch);
            await SendNotificationsAsync(
                bmIds,
                "Resignation Rejected by Area Manager ❌",
                $"Resignation request #{entity.Id} for {entity.EmployeeName} was rejected by Area Manager. Reason: {comments}",
                CoreNotificationType.Rejected,
                entity.Id,
                $"/BranchManager/ReviewResignation/{entity.Id}"
            );

            var hrOfficerIds = await GetHROfficerUserIdentifiersAsync(entity.Branch);
            await SendNotificationsAsync(
                hrOfficerIds,
                "Resignation Rejected by Area Manager ❌",
                $"Resignation request #{entity.Id} for {entity.EmployeeName} was rejected by Area Manager. Reason: {comments}",
                CoreNotificationType.Rejected,
                entity.Id,
                $"/HRManager/ReviewResignation/{entity.Id}"
            );

            return true;
        }

        // ── Stage 5: HR Officer / HR Manager Finalization ─────────────────────
        public async Task<List<ResignationRequestViewModel>> GetPendingForHRManagerAsync(List<int>? managedBranchIds = null)
        {
            var query = _context.ResignationRequests
                .AsSplitQuery()
                .Include(r => r.Documents)
                .Include(r => r.DepartmentReviews)
                .Where(r => r.Status == ResignationStatus.AMApproved);

            if (managedBranchIds != null && managedBranchIds.Any())
            {
                var branchNames = await _context.Branches
                    .Where(b => managedBranchIds.Contains(b.Id))
                    .Select(b => b.Name.ToLower())
                    .ToListAsync();

                query = query.Where(r => r.Branch != null && branchNames.Contains(r.Branch.ToLower()));
            }

            var list = await query.OrderByDescending(r => r.AMReviewDate).ToListAsync();
            return list.Select(Map).ToList();
        }

        public async Task<List<ResignationRequestViewModel>> GetReviewedByHRManagerAsync(List<int>? managedBranchIds = null)
        {
            var query = _context.ResignationRequests
                .AsSplitQuery()
                .Include(r => r.Documents)
                .Include(r => r.DepartmentReviews)
                .Where(r => r.HRReview != null);

            if (managedBranchIds != null && managedBranchIds.Any())
            {
                var branchNames = await _context.Branches
                    .Where(b => managedBranchIds.Contains(b.Id))
                    .Select(b => b.Name.ToLower())
                    .ToListAsync();

                query = query.Where(r => r.Branch != null && branchNames.Contains(r.Branch.ToLower()));
            }

            var list = await query.OrderByDescending(r => r.HRReviewDate).ToListAsync();
            return list.Select(Map).ToList();
        }

        public async Task<bool> HRManagerApproveAsync(int id, string comments, string reviewerEmail)
        {
            var entity = await _context.ResignationRequests
                .Include(r => r.DepartmentReviews)
                .FirstOrDefaultAsync(r => r.Id == id);

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

            // 1. Notify Employee (Acceptance Letter ready)
            var empIds = await GetEmployeeUserIdentifiersAsync(entity.EmployeeEmail, entity.EpfNumber);
            await SendNotificationsAsync(
                empIds,
                "Resignation Approved & Acceptance Letter Available ✅",
                $"Your resignation has been officially finalized and approved by HR. Your acceptance letter is now available. Last working day: {entity.EffectiveDate:MMMM dd, yyyy}.",
                CoreNotificationType.Approved,
                entity.Id,
                $"/Resignation/AcceptanceLetter/{entity.Id}"
            );

            // 2. Notify Department Heads
            var deptHeadIds = await GetDepartmentHeadUserIdentifiersAsync(entity.Branch);
            await SendNotificationsAsync(
                deptHeadIds,
                "Resignation Finalized by HR ✅",
                $"Resignation request #{entity.Id} for {entity.EmployeeName} ({entity.Branch}) has been fully approved and finalized by HR.",
                CoreNotificationType.Approved,
                entity.Id,
                $"/DepartmentHead/ReviewResignation/{entity.Id}"
            );

            // 3. Notify Branch Manager
            var bmIds = await GetBranchManagerUserIdentifiersAsync(entity.Branch);
            await SendNotificationsAsync(
                bmIds,
                "Resignation Finalized by HR ✅",
                $"Resignation request #{entity.Id} for {entity.EmployeeName} has been finalized and approved by HR.",
                CoreNotificationType.Approved,
                entity.Id,
                $"/BranchManager/ReviewResignation/{entity.Id}"
            );

            // 4. Notify Area Manager
            var amIds = await GetAreaManagerUserIdentifiersAsync(entity.Branch);
            await SendNotificationsAsync(
                amIds,
                "Resignation Finalized by HR ✅",
                $"Resignation request #{entity.Id} for {entity.EmployeeName} has been finalized and approved by HR.",
                CoreNotificationType.Approved,
                entity.Id,
                $"/AreaManager/ReviewResignation/{entity.Id}"
            );

            // 5. Notify HR Officers
            var hrOfficerIds = await GetHROfficerUserIdentifiersAsync(entity.Branch);
            await SendNotificationsAsync(
                hrOfficerIds,
                "Resignation Finalized by HR ✅",
                $"Resignation request #{entity.Id} for {entity.EmployeeName} has been officially approved. Please coordinate offboarding on effective date ({entity.EffectiveDate:MMMM dd, yyyy}).",
                CoreNotificationType.Approved,
                entity.Id,
                $"/HRManager/ReviewResignation/{entity.Id}"
            );

            return true;
        }

        public async Task<bool> HRManagerRejectAsync(int id, string comments, string reviewerEmail)
        {
            var entity = await _context.ResignationRequests
                .Include(r => r.DepartmentReviews)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (entity == null || entity.Status != ResignationStatus.AMApproved)
                return false;

            entity.HRReview     = "Rejected";
            entity.HRReviewDate = DateTime.Now;
            entity.HRComments   = comments;
            entity.HREmail      = reviewerEmail;
            entity.Status       = ResignationStatus.HRRejected;
            entity.LastModifiedDate = DateTime.Now;

            await _context.SaveChangesAsync();

            // Notify Employee
            var empIds = await GetEmployeeUserIdentifiersAsync(entity.EmployeeEmail, entity.EpfNumber);
            await SendNotificationsAsync(
                empIds,
                "Resignation Rejected by HR ❌",
                $"Your resignation request #{entity.Id} was rejected by HR. Reason: {comments}",
                CoreNotificationType.Rejected,
                entity.Id,
                $"/Resignation/Details/{entity.Id}"
            );

            // Notify Department Heads, BM, AM, HR Officers
            var deptHeadIds = await GetDepartmentHeadUserIdentifiersAsync(entity.Branch);
            await SendNotificationsAsync(
                deptHeadIds,
                "Resignation Rejected by HR ❌",
                $"Resignation request #{entity.Id} for {entity.EmployeeName} was rejected at HR finalization. Reason: {comments}",
                CoreNotificationType.Rejected,
                entity.Id,
                $"/DepartmentHead/ReviewResignation/{entity.Id}"
            );

            var bmIds = await GetBranchManagerUserIdentifiersAsync(entity.Branch);
            await SendNotificationsAsync(
                bmIds,
                "Resignation Rejected by HR ❌",
                $"Resignation request #{entity.Id} for {entity.EmployeeName} was rejected at HR finalization. Reason: {comments}",
                CoreNotificationType.Rejected,
                entity.Id,
                $"/BranchManager/ReviewResignation/{entity.Id}"
            );

            var amIds = await GetAreaManagerUserIdentifiersAsync(entity.Branch);
            await SendNotificationsAsync(
                amIds,
                "Resignation Rejected by HR ❌",
                $"Resignation request #{entity.Id} for {entity.EmployeeName} was rejected at HR finalization. Reason: {comments}",
                CoreNotificationType.Rejected,
                entity.Id,
                $"/AreaManager/ReviewResignation/{entity.Id}"
            );

            var hrOfficerIds = await GetHROfficerUserIdentifiersAsync(entity.Branch);
            await SendNotificationsAsync(
                hrOfficerIds,
                "Resignation Rejected by HR ❌",
                $"Resignation request #{entity.Id} for {entity.EmployeeName} was rejected at HR finalization. Reason: {comments}",
                CoreNotificationType.Rejected,
                entity.Id,
                $"/HRManager/ReviewResignation/{entity.Id}"
            );

            return true;
        }

        // ── General Queries ───────────────────────────────────────────────────
        public async Task<ResignationRequestViewModel?> GetByIdAsync(int id)
        {
            var entity = await _context.ResignationRequests
                .AsSplitQuery()
                .Include(r => r.Documents)
                .Include(r => r.DepartmentReviews)
                .FirstOrDefaultAsync(r => r.Id == id);
            return entity == null ? null : Map(entity);
        }

        public async Task<List<ResignationRequestViewModel>> GetMyResignationsAsync(string employeeEmail)
        {
            if (string.IsNullOrWhiteSpace(employeeEmail)) return new List<ResignationRequestViewModel>();

            var eKey = employeeEmail.Trim().ToLower();

            var list = await _context.ResignationRequests
                .AsSplitQuery()
                .Include(r => r.Documents)
                .Include(r => r.DepartmentReviews)
                .Where(r => (r.EmployeeEmail != null && r.EmployeeEmail.ToLower() == eKey) ||
                            (r.InitiatedBy != null && r.InitiatedBy.ToLower() == eKey))
                .OrderByDescending(r => r.CreatedDate)
                .ToListAsync();

            return list.Select(Map).ToList();
        }

        public async Task<List<ResignationRequestViewModel>> GetAllAsync(string? statusFilter = null, string? search = null)
        {
            var query = _context.ResignationRequests
                .AsSplitQuery()
                .Include(r => r.Documents)
                .Include(r => r.DepartmentReviews)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(statusFilter) &&
                Enum.TryParse<ResignationStatus>(statusFilter, out var status))
                query = query.Where(r => r.Status == status);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.ToLower();
                query = query.Where(r =>
                    (r.EmployeeName != null && r.EmployeeName.ToLower().Contains(s)) ||
                    (r.EpfNumber != null && r.EpfNumber.ToLower().Contains(s)) ||
                    (r.EmployeeEmail != null && r.EmployeeEmail.ToLower().Contains(s)) ||
                    (r.Branch != null && r.Branch.ToLower().Contains(s)));
            }

            var list = await query.OrderByDescending(r => r.CreatedDate).ToListAsync();
            return list.Select(Map).ToList();
        }

        // ── Process Effective Date & Reactivation ─────────────────────────────
        public async Task<(bool Success, string? Error)> ProcessEffectiveDateAsync(
            int id, string processedBy, UserManager<ApplicationUser> userManager)
        {
            var entity = await _context.ResignationRequests.FindAsync(id);
            if (entity == null) return (false, "Request not found.");
            if (entity.Status != ResignationStatus.HRApproved) return (false, "Only HR-approved resignations can be processed.");
            if (entity.AccountDeactivated) return (false, "Account has already been deactivated.");
            if (DateTime.Today < entity.EffectiveDate.Date) return (false, $"Effective date is {entity.EffectiveDate:MMMM dd, yyyy}. Cannot process before that date.");

            var user = await userManager.FindByEmailAsync(entity.EmployeeEmail) ??
                       await userManager.FindByNameAsync(entity.EmployeeEmail);
            if (user != null)
            {
                user.LockoutEnabled = true;
                user.LockoutEnd = DateTimeOffset.MaxValue;
                await userManager.UpdateAsync(user);
            }

            entity.AccountDeactivated    = true;
            entity.AccountDeactivatedDate = DateTime.Now;
            entity.AccountDeactivatedBy  = processedBy;
            entity.Status                = ResignationStatus.Completed;
            entity.LastModifiedDate      = DateTime.Now;

            await _context.SaveChangesAsync();

            var empIds = await GetEmployeeUserIdentifiersAsync(entity.EmployeeEmail, entity.EpfNumber);
            await SendNotificationsAsync(
                empIds,
                "Employment Ended – Account Deactivated",
                $"Your employment has officially ended on {entity.EffectiveDate:MMMM dd, yyyy}. Your account has been deactivated.",
                CoreNotificationType.Info,
                entity.Id,
                $"/Resignation/Details/{entity.Id}"
            );

            return (true, null);
        }

        public async Task<(bool Success, string? Error)> ReactivateAccountAsync(
            int id, string reactivatedBy, UserManager<ApplicationUser> userManager)
        {
            var entity = await _context.ResignationRequests.FindAsync(id);
            if (entity == null) return (false, "Request not found.");
            if (!entity.AccountDeactivated) return (false, "Account is not deactivated.");

            var user = await userManager.FindByEmailAsync(entity.EmployeeEmail) ??
                       await userManager.FindByNameAsync(entity.EmployeeEmail);
            if (user != null)
            {
                user.LockoutEnd = null;
                user.LockoutEnabled = false;
                await userManager.UpdateAsync(user);
            }

            entity.AccountDeactivated    = false;
            entity.AccountDeactivatedDate = null;
            entity.AccountDeactivatedBy  = null;
            entity.LastModifiedDate      = DateTime.Now;

            await _context.SaveChangesAsync();

            var empIds = await GetEmployeeUserIdentifiersAsync(entity.EmployeeEmail, entity.EpfNumber);
            await SendNotificationsAsync(
                empIds,
                "Account Reactivated",
                $"Your account has been reactivated by {reactivatedBy}. You can now log in to the HRMS portal.",
                CoreNotificationType.Approved,
                entity.Id,
                $"/Resignation/Details/{entity.Id}"
            );

            return (true, null);
        }

        // ── Documents ─────────────────────────────────────────────────────────
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

        // ── Notification Helpers ─────────────────────────────────────────────
        private async Task<List<string>> GetDepartmentHeadUserIdentifiersAsync(string? branchName, string? deptName = null)
        {
            if (string.IsNullOrWhiteSpace(branchName)) return new List<string>();

            var role = await _context.Roles.FirstOrDefaultAsync(r => r.Name == "Department Head");
            if (role == null) return new List<string>();

            var bKey = branchName.Trim().ToLower();
            var dKey = deptName?.Trim().ToLower();

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
                    (string.IsNullOrEmpty(dKey) ||
                     (!string.IsNullOrEmpty(x.uDept) && x.uDept.Trim().ToLower() == dKey) ||
                     (!string.IsNullOrEmpty(x.empDept) && x.empDept.Trim().ToLower() == dKey)))
                .Select(x => x.Id)
                .Distinct()
                .ToList();
        }

        private async Task<List<string>> GetBranchManagerUserIdentifiersAsync(string? branchName)
        {
            if (string.IsNullOrWhiteSpace(branchName)) return new List<string>();

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

        private async Task<List<string>> GetAreaManagerUserIdentifiersAsync(string? branchName)
        {
            var role = await _context.Roles.FirstOrDefaultAsync(r => r.Name == "Area Manager");
            if (role == null) return new List<string>();

            int branchId = 0;
            if (!string.IsNullOrWhiteSpace(branchName))
            {
                var b = await _context.Branches.FirstOrDefaultAsync(b => b.Name.ToLower() == branchName.Trim().ToLower());
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
            if (role == null) return new List<string>();

            int branchId = 0;
            if (!string.IsNullOrWhiteSpace(branchName))
            {
                var b = await _context.Branches.FirstOrDefaultAsync(b => b.Name.ToLower() == branchName.Trim().ToLower());
                if (b != null) branchId = b.Id;
            }

            var bKey = branchName?.Trim().ToLower() ?? "";

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
                        (!string.IsNullOrEmpty(bKey) && u.Branch.Trim().ToLower() == bKey))
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
            int resignationRequestId,
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
                    await _notificationService.CreateNotificationAsync(recipient, title, message, type, resignationRequestId, targetUrl);
                }
                catch
                {
                    // Prevent individual notification failure from stopping the workflow
                }
            }
        }

        // ── Mapper ────────────────────────────────────────────────────────────
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
            }).ToList(),
            DepartmentReviews = e.DepartmentReviews.Select(dr => new ResignationDepartmentReviewViewModel
            {
                Id                   = dr.Id,
                ResignationRequestId = dr.ResignationRequestId,
                DepartmentId         = dr.DepartmentId,
                DepartmentName       = dr.DepartmentName,
                ReviewerUserId       = dr.ReviewerUserId,
                ReviewerName         = dr.ReviewerName,
                ReviewerEmail        = dr.ReviewerEmail,
                Status               = dr.Status,
                Comments             = dr.Comments,
                ReviewDate           = dr.ReviewDate
            }).ToList()
        };
    }
}
