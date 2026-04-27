using HRMS.Domain.Entities.Welfare;
using HRMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HRMS.UI.Pages.Welfare
{
    [Authorize]
    public class RequestFormModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;

        public RequestFormModel(ApplicationDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        // ── Bound form properties ─────────────────────────────────────────────
        [BindProperty] public int EmployeeId { get; set; }
        [BindProperty] public int WelfareTypeId { get; set; }
        [BindProperty] public string RequestDate { get; set; } = string.Empty;
        [BindProperty] public decimal RequestedAmount { get; set; }
        [BindProperty] public string Remark { get; set; } = string.Empty;
        [BindProperty] public string Urgency { get; set; } = "Normal";
        [BindProperty] public bool IsDraft { get; set; }
        [BindProperty] public List<IFormFile>? Documents { get; set; }

        // ── Profile data shown in the header ─────────────────────────────────
        public CurrentUserProfile CurrentUser { get; set; } = new();

        public async Task OnGetAsync()
        {
            await LoadCurrentUserAsync();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            // Re-load profile so the header renders correctly if we return Page()
            await LoadCurrentUserAsync();

            try
            {
                // ── 1. Save the welfare request ───────────────────────────────
                var request = new WelfareRequest
                {
                    EmployeeId = EmployeeId,
                    WelfareTypeId = WelfareTypeId,
                    RequestDate = DateTime.Parse(RequestDate),
                    RequestedAmount = RequestedAmount,
                    Remark = Remark,
                    IsDraft = IsDraft,
                    Status = IsDraft ? "Draft" : "Pending",
                    CurrentLevel = "BranchDGM",
                    CurrentStatus = "Pending",
                    CreatedAt = DateTime.Now,
                    SubmittedBy = EmployeeId
                };

                _context.WelfareRequests.Add(request);
                await _context.SaveChangesAsync();

                // ── 2. Save uploaded documents ────────────────────────────────
                if (Documents != null && Documents.Count > 0)
                {
                    // Resolve web root — fall back if WebRootPath is null
                    var webRoot = _env.WebRootPath;
                    if (string.IsNullOrEmpty(webRoot))
                    {
                        webRoot = Path.Combine(_env.ContentRootPath, "wwwroot");
                    }

                    var folderPath = Path.Combine(
                        webRoot, "uploads", "welfare",
                        request.RequestId.ToString());

                    Directory.CreateDirectory(folderPath);

                    var allowed = new[] { ".pdf", ".doc", ".docx", ".jpg", ".jpeg", ".png" };

                    foreach (var file in Documents)
                    {
                        if (file.Length <= 0) continue;
                        if (file.Length > 5 * 1024 * 1024) continue;

                        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
                        if (string.IsNullOrEmpty(ext) || !allowed.Contains(ext)) continue;

                        var uniqueName = Guid.NewGuid().ToString("N") + ext;
                        var savePath = Path.Combine(folderPath, uniqueName);

                        using (var stream = new FileStream(savePath, FileMode.Create))
                        {
                            await file.CopyToAsync(stream);
                        }

                        _context.WelfareDocuments.Add(new WelfareDocument
                        {
                            RequestId = request.RequestId,
                            FileName = file.FileName,
                            FilePath = $"/uploads/welfare/{request.RequestId}/{uniqueName}",
                            FileType = file.ContentType,
                            UploadedAt = DateTime.Now
                        });
                    }

                    await _context.SaveChangesAsync();
                }

                TempData["Success"] = IsDraft
                    ? "Draft saved successfully!"
                    : "Request submitted successfully!";

                return RedirectToPage("/Welfare/RequestList");
            }
            catch (Exception ex)
            {
                TempData["Error"] = "An error occurred: " + ex.Message;
                return Page();
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private async Task LoadCurrentUserAsync()
        {
            // ASP.NET Identity stores the username as the user's email / username.
            // We look up the Employee row whose Email matches the logged-in username.
            var username = User.Identity?.Name;
            if (string.IsNullOrEmpty(username)) return;

            var emp = await _context.Employees
                .Include(e => e.Designation)
                .Include(e => e.Department)
                .Include(e => e.Branch)
                .FirstOrDefaultAsync(e => e.Email == username);

            if (emp == null) return;

            CurrentUser = new CurrentUserProfile
            {
                FullName = $"{emp.FirstName} {emp.LastName}".Trim(),
                Initials = BuildInitials(emp.FirstName, emp.LastName),
                Designation = emp.Designation?.Title ?? string.Empty,
                Department = emp.Department?.Name ?? string.Empty,
                EmployeeCode = $"EMP-{emp.Id:D5}",
                Status = emp.Status ?? "Active",
                PhotoUrl = null   // set to a real URL/path if you store photos later
            };
        }

        private static string BuildInitials(string first, string last)
        {
            var f = string.IsNullOrWhiteSpace(first) ? "" : first[0].ToString().ToUpper();
            var l = string.IsNullOrWhiteSpace(last) ? "" : last[0].ToString().ToUpper();
            return f + l;
        }
    }

    // ── DTO ───────────────────────────────────────────────────────────────────
    public class CurrentUserProfile
    {
        public string FullName { get; set; } = string.Empty;
        public string Initials { get; set; } = "?";
        public string Designation { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public string EmployeeCode { get; set; } = string.Empty;
        public string Status { get; set; } = "Active";
        public string? PhotoUrl { get; set; }
    }
}
