using HRMS.Infrastructure.Identity;
using HRMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;

namespace HRMS.UI.Pages.Welfare
{
    [Authorize]
    public class DownloadDocumentModel : PageModel
    {
        private readonly ApplicationDbContext _db;
        private readonly IWebHostEnvironment _env;
        private readonly UserManager<ApplicationUser> _userManager;

        // Roles that are allowed to view ANY welfare document (approvers in the workflow).
        // Add or remove roles here if your approval chain changes.
        private static readonly string[] ApproverRoles = new[]
        {
            "BranchDGM",
            "HODGM",
            "SeniorManagement",
            "Finance"
        };

        public DownloadDocumentModel(
            ApplicationDbContext db,
            IWebHostEnvironment env,
            UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _env = env;
            _userManager = userManager;
        }

        /// <summary>
        /// Serves a welfare document either inline (view in browser) or as an attachment (force download).
        /// Access rules:
        ///   - The employee who owns the welfare request (submitter) can always access their own documents.
        ///   - Users with approver roles (BranchDGM, HODGM, SeniorManagement, Finance) can access any document.
        ///   - Everyone else gets 403 Forbid.
        /// Usage:
        ///   /Welfare/DownloadDocument?id=5             → force download
        ///   /Welfare/DownloadDocument?id=5&mode=view   → open in browser (for PDFs / images)
        /// </summary>
        public async Task<IActionResult> OnGetAsync(int id, string? mode = null)
        {
            // 1. Look up the document along with its parent request (needed for owner check)
            var doc = await _db.WelfareDocuments
                .Include(d => d.WelfareRequest)
                .FirstOrDefaultAsync(d => d.DocumentId == id);

            if (doc == null) return NotFound("Document not found.");

            // 2. Access control — must be owner or approver
            if (!await CanAccessAsync(doc.WelfareRequest.EmployeeId))
            {
                return Forbid();
            }

            // 3. Resolve the physical file path on disk
            var webRoot = _env.WebRootPath;
            if (string.IsNullOrEmpty(webRoot))
            {
                webRoot = Path.Combine(_env.ContentRootPath, "wwwroot");
            }

            var relative = doc.FilePath.TrimStart('/', '\\').Replace('/', Path.DirectorySeparatorChar);
            var physicalPath = Path.Combine(webRoot, relative);

            if (!System.IO.File.Exists(physicalPath))
                return NotFound("File is missing on disk.");

            // 4. Figure out the content type
            var provider = new FileExtensionContentTypeProvider();
            if (!provider.TryGetContentType(physicalPath, out var contentType))
            {
                contentType = doc.FileType ?? "application/octet-stream";
            }

            var bytes = await System.IO.File.ReadAllBytesAsync(physicalPath);

            // 5. Serve — view mode = inline, default = download
            if (string.Equals(mode, "view", StringComparison.OrdinalIgnoreCase))
            {
                Response.Headers["Content-Disposition"] = $"inline; filename=\"{doc.FileName}\"";
                return File(bytes, contentType);
            }

            return File(bytes, contentType, doc.FileName);
        }

        /// <summary>
        /// Returns true if the current user is either the employee who owns the request,
        /// or a user in one of the approver roles.
        /// </summary>
        private async Task<bool> CanAccessAsync(int requestOwnerEmployeeId)
        {
            // Approvers can access any document
            if (User.IsInRole("BranchDGM") || User.IsInRole("HODGM") ||
                User.IsInRole("SeniorManagement") || User.IsInRole("Finance"))
            {
                return true;
            }

            // Otherwise, must be the owner of the request
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return false;

            var employee = await _db.Employees
                .FirstOrDefaultAsync(e => e.Email == user.Email);

            return employee != null && employee.Id == requestOwnerEmployeeId;
        }
    }
}
