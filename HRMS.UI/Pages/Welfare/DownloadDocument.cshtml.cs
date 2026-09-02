using HRMS.Infrastructure.Identity;
using HRMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;

namespace HRMS.UI.Pages.Welfare
{
    [Authorize]
    public class DownloadDocumentModel : PageModel
    {
        private readonly ApplicationDbContext _db;
        private readonly IWebHostEnvironment _env;
        private readonly UserManager<ApplicationUser> _userManager;

        public DownloadDocumentModel(
            ApplicationDbContext db,
            IWebHostEnvironment env,
            UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _env = env;
            _userManager = userManager;
        }

        public async Task<IActionResult> OnGetAsync(int id, string? mode = null)
        {
            var doc = await _db.WelfareDocuments
                .Include(d => d.WelfareRequest)
                .FirstOrDefaultAsync(d => d.DocumentId == id);

            if (doc == null) return NotFound("Document not found.");

            if (!await CanAccessAsync(doc.WelfareRequest.EmployeeId))
            {
                return Forbid();
            }

            var webRoot = _env.WebRootPath;
            if (string.IsNullOrEmpty(webRoot))
            {
                webRoot = Path.Combine(_env.ContentRootPath, "wwwroot");
            }

            var relative = doc.FilePath.TrimStart('/', '\\').Replace('/', Path.DirectorySeparatorChar);
            var physicalPath = Path.Combine(webRoot, relative);

            if (!System.IO.File.Exists(physicalPath))
                return NotFound("File is missing on disk.");

            var provider = new FileExtensionContentTypeProvider();
            if (!provider.TryGetContentType(physicalPath, out var contentType))
            {
                contentType = doc.FileType ?? "application/octet-stream";
            }

            var bytes = await System.IO.File.ReadAllBytesAsync(physicalPath);

            if (string.Equals(mode, "view", StringComparison.OrdinalIgnoreCase))
            {
                Response.Headers["Content-Disposition"] = $"inline; filename=\"{doc.FileName}\"";
                return File(bytes, contentType);
            }

            return File(bytes, contentType, doc.FileName);
        }

        private async Task<bool> CanAccessAsync(int requestOwnerEmployeeId)
        {
            if (User.IsInRole("Welfare Manager") || User.IsInRole("Department Head") || User.IsInRole("Branch Manager") ||
                User.IsInRole("Area Manager") || User.IsInRole("HR Manager") || User.IsInRole("HR Officer") || User.IsInRole("Admin"))
            {
                return true;
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return false;

            var employee = await _db.Employees
                .FirstOrDefaultAsync(e => e.Email == user.Email && !e.NIC.StartsWith("DUTY") && e.NIC != "DUTY-ACC");

            return employee != null && employee.Id == requestOwnerEmployeeId;
        }
    }
}
