using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using HRMS.Domain.Entities.Core;
using HRMS.Infrastructure.Identity;
using HRMS.Infrastructure.Persistence;

namespace HRMS.UI.Pages.Employees
{
    using Employee = HRMS.Domain.Entities.Core.Employee;

    [Authorize(Roles = "HR Manager,HR Officer,Area Manager,Branch Manager,Admin")]

    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public IndexModel(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public IList<Employee> EmployeeList { get; set; } = new List<Employee>();
        public IList<DraftEmployee> DraftList { get; set; } = new List<DraftEmployee>();
        public IList<PendingDocumentItem> PendingDocumentList { get; set; } = new List<PendingDocumentItem>();

        public int TotalEmployees { get; set; }
        public int DraftCount { get; set; }
        public int PendingApprovals { get; set; }

        // Pagination
        public const int PageSize = 5;
        [BindProperty(SupportsGet = true)]
        public int PageNumber { get; set; } = 1;
        public int TotalPages { get; set; }

        // Current active tab
        [BindProperty(SupportsGet = true)]
        public string Tab { get; set; } = "directory";

        public async Task OnGetAsync()
        {
            // scopedBranchId: single branch for Branch Manager
            int? scopedBranchId = null;
            List<int>? allowedBranchIds = null;

            var currentUser = await _userManager.GetUserAsync(User);

            if (User.IsInRole("Admin") || User.IsInRole("HR Manager"))
            {
                // Full global company-wide access to all branches
                scopedBranchId = null;
                allowedBranchIds = null;
            }
            else if (User.IsInRole("HR Officer"))
            {
                // HR Officer assigned branches
                if (!string.IsNullOrEmpty(currentUser?.ManagedBranches))
                {
                    allowedBranchIds = currentUser.ManagedBranches
                        .Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(s => int.TryParse(s.Trim(), out var id) ? id : 0)
                        .Where(id => id > 0)
                        .ToList();
                }
                if (allowedBranchIds == null || !allowedBranchIds.Any())
                {
                    allowedBranchIds = new List<int> { -1 };
                }
            }
            else if (User.IsInRole("Branch Manager"))
            {
                if (!string.IsNullOrWhiteSpace(currentUser?.Branch))
                {
                    var branch = await _context.Branches.FirstOrDefaultAsync(b => b.Name == currentUser.Branch);
                    scopedBranchId = branch?.Id ?? -1;
                }
                else
                {
                    scopedBranchId = -1;
                }
            }
            else if (User.IsInRole("Area Manager"))
            {
                var managed = currentUser?.ManagedBranches ?? "";
                allowedBranchIds = managed
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => int.TryParse(s.Trim(), out var id) ? id : 0)
                    .Where(id => id > 0)
                    .ToList();
                    
                if (!allowedBranchIds.Any()) allowedBranchIds = new List<int> { -1 };
            }

            var employeeQuery = _context.Employees
                .Include(e => e.Department)
                .Include(e => e.Designation)
                .Where(e => e.Status != "Draft" && !e.NIC.StartsWith("DUTY") && e.NIC != "DUTY-ACC");
            if (scopedBranchId.HasValue)
                employeeQuery = employeeQuery.Where(e => e.BranchId == scopedBranchId.Value);
            else if (allowedBranchIds != null)
                employeeQuery = employeeQuery.Where(e => allowedBranchIds.Contains(e.BranchId));
            var activeEmployees = await employeeQuery.ToListAsync();

            var draftQuery = _context.DraftEmployees
                .Include(e => e.Department)
                .Include(e => e.Designation);
            IQueryable<DraftEmployee> draftFiltered = draftQuery;
            if (scopedBranchId.HasValue)
                draftFiltered = draftFiltered.Where(e => !e.BranchId.HasValue || e.BranchId == scopedBranchId.Value);
            else if (allowedBranchIds != null)
                draftFiltered = draftFiltered.Where(e => !e.BranchId.HasValue || allowedBranchIds.Contains(e.BranchId.Value));
            var draftedEmployees = await draftFiltered.OrderByDescending(d => d.LastUpdated).ToListAsync();

            // Sanitize active tab for roles without document approval access
            if (Tab == "documents" && !User.IsInRole("HR Officer") && !User.IsInRole("Admin"))
            {
                Tab = "directory";
            }

            // Build pending document list with employee names (only for HR Officers and Admins)
            if (User.IsInRole("HR Officer") || User.IsInRole("Admin"))
            {
                var docQuery = _context.EmployeeDocuments
                    .Where(d => d.Status == "Pending")
                    .Include(d => d.Employee)
                    .OrderBy(d => d.UploadedAt);
                IQueryable<EmployeeDocument> docFiltered = docQuery;
                if (scopedBranchId.HasValue)
                    docFiltered = docFiltered.Where(d => d.Employee != null && d.Employee.BranchId == scopedBranchId.Value);
                else if (allowedBranchIds != null)
                    docFiltered = docFiltered.Where(d => d.Employee != null && allowedBranchIds.Contains(d.Employee.BranchId));
                var pendingDocs = await docFiltered.ToListAsync();

                PendingDocumentList = pendingDocs.Select(d => new PendingDocumentItem
                {
                    DocumentId     = d.Id,
                    EmployeeId     = d.EmployeeId,
                    EmployeeName   = d.Employee?.FullName ?? "Unknown",
                    DocumentType   = d.DocumentType,
                    FileName       = d.FileName,
                    StoredFileName = d.StoredFileName,
                    ContentType    = d.ContentType,
                    UploadedAt     = d.UploadedAt
                }).ToList();

                PendingApprovals = PendingDocumentList.Count;
            }

            TotalEmployees   = activeEmployees.Count;
            TotalPages       = (int)Math.Ceiling(TotalEmployees / (double)PageSize);
            if (PageNumber < 1) PageNumber = 1;
            if (PageNumber > TotalPages && TotalPages > 0) PageNumber = TotalPages;

            EmployeeList = activeEmployees;
            DraftList = draftedEmployees;
            DraftCount = DraftList.Count;

            // --- Alert HR Managers 1 month before probation/internship end ---
            if (User.IsInRole("HR Manager") || User.IsInRole("Admin"))
            {
                var today = DateTime.Today;
                var alertWindow = today.AddDays(30);
                var currentUserId = _userManager.GetUserId(User);

                var atRiskEmployees = activeEmployees.Where(e =>
                {
                    if (e.DateJoined == null) return false;
                    if (e.EmployeeType == "Probationary" && e.ProbationPeriodMonths > 0)
                    {
                        var endDate = e.DateJoined.Value.AddMonths(e.ProbationPeriodMonths);
                        return endDate >= today && endDate <= alertWindow;
                    }
                    if (e.EmployeeType == "Intern" && e.InternPeriodMonths > 0)
                    {
                        var endDate = e.DateJoined.Value.AddMonths(e.InternPeriodMonths);
                        return endDate >= today && endDate <= alertWindow;
                    }
                    return false;
                }).ToList();

                foreach (var emp in atRiskEmployees)
                {
                    var periodType = emp.EmployeeType == "Probationary" ? "Probation" : "Internship";
                    var months     = emp.EmployeeType == "Probationary" ? emp.ProbationPeriodMonths : emp.InternPeriodMonths;
                    var endDate    = emp.DateJoined!.Value.AddMonths(months);
                    var daysLeft   = (int)(endDate - today).TotalDays;
                    var notifTitle = $"{periodType} Ending: {emp.FullName}";

                    // Deduplicate — only create if no unread notification with this title already exists for this user
                    var alreadyNotified = await _context.Notifications
                        .AnyAsync(n => n.UserId == currentUserId && n.Title == notifTitle && !n.IsRead);

                    if (!alreadyNotified)
                    {
                        _context.Notifications.Add(new Notification
                        {
                            UserId    = currentUserId!,
                            Title     = notifTitle,
                            Message   = $"{emp.FullName}'s {periodType.ToLower()} period ends on {endDate:dd MMM yyyy} ({daysLeft} day{(daysLeft == 1 ? "" : "s")} remaining). Please take the necessary action.",
                            TargetUrl = $"/Employees/Details/{emp.Id}",
                            IsRead    = false,
                            CreatedAt = HRMS.Domain.Common.SriLankaTime.Now,
                            Type      = CoreNotificationType.Info
                        });
                    }
                }

                if (atRiskEmployees.Any())
                    await _context.SaveChangesAsync();
            }
        }

        // Delete a drafted employee
        public async Task<IActionResult> OnPostDeleteDraftAsync(int id)
        {
            var draft = await _context.DraftEmployees.FindAsync(id);
            if (draft != null)
            {
                _context.DraftEmployees.Remove(draft);
                await _context.SaveChangesAsync();
            }
            return RedirectToPage(new { tab = "drafts" });
        }

        // Approve a document
        public async Task<IActionResult> OnPostApproveDocumentAsync(int documentId)
        {
            var doc = await _context.EmployeeDocuments.FindAsync(documentId);
            if (doc != null)
            {
                doc.Status           = "Approved";
                doc.ReviewedAt       = DateTime.Now;
                doc.ReviewedByUserId = _userManager.GetUserId(User);
                await _context.SaveChangesAsync();
            }
            return RedirectToPage(new { tab = "documents" });
        }

        // Reject a document
        public async Task<IActionResult> OnPostRejectDocumentAsync(int documentId, string? rejectionNotes)
        {
            var doc = await _context.EmployeeDocuments.FindAsync(documentId);
            if (doc != null)
            {
                doc.Status           = "Rejected";
                doc.ReviewedAt       = DateTime.Now;
                doc.ReviewedByUserId = _userManager.GetUserId(User);
                doc.ReviewerNotes    = rejectionNotes;
                await _context.SaveChangesAsync();
            }
            return RedirectToPage(new { tab = "documents" });
        }

        // DTO for the pending document table
        public class PendingDocumentItem
        {
            public int DocumentId { get; set; }
            public int EmployeeId { get; set; }
            public string EmployeeName { get; set; } = "";
            public string DocumentType { get; set; } = "";
            public string FileName { get; set; } = "";
            public string StoredFileName { get; set; } = "";
            public string ContentType { get; set; } = "";
            public DateTime UploadedAt { get; set; }
        }
    }
}
