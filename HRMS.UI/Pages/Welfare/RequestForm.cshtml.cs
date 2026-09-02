using HRMS.Application.Services;
using HRMS.Domain.Entities.Core;
using HRMS.Domain.Entities.Welfare;
using HRMS.Infrastructure.Identity;
using HRMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace HRMS.UI.Pages.Welfare
{
    using Employee = HRMS.Domain.Entities.Core.Employee;
    [Authorize]
    public class RequestFormModel : BasePageModel
    {
        private readonly IWebHostEnvironment _env;
        private readonly INotificationService _notifService;
        private readonly UserManager<ApplicationUser> _userManager;

        public RequestFormModel(
            ApplicationDbContext context, 
            IWebHostEnvironment env,
            INotificationService notifService,
            UserManager<ApplicationUser> userManager)
            : base(context)
        {
            _env = env;
            _notifService = notifService;
            _userManager = userManager;
        }

        [BindProperty] public int EmployeeId { get; set; }
        [BindProperty] public int WelfareTypeId { get; set; }
        [BindProperty] public string RequestDate { get; set; } = string.Empty;
        [BindProperty] public decimal RequestedAmount { get; set; }
        [BindProperty] public string Remark { get; set; } = string.Empty;
        [BindProperty] public string Urgency { get; set; } = "Normal";
        [BindProperty] public bool IsDraft { get; set; }
        [BindProperty] public List<IFormFile>? Documents { get; set; }

        private async Task<Employee?> GetCurrentEmployeeAsync()
        {
            var username = User.Identity?.Name;
            var userAccount = await _db.Users.FirstOrDefaultAsync(u => u.UserName == username || u.Email == username);
            Employee? employee = null;
            if (userAccount?.EmployeeId.HasValue == true)
            {
                employee = await _db.Employees
                    .FirstOrDefaultAsync(e => e.Id == userAccount.EmployeeId.Value && !e.NIC.StartsWith("DUTY") && e.NIC != "DUTY-ACC");
            }
            if (employee == null && !string.IsNullOrEmpty(userAccount?.Email))
            {
                employee = await _db.Employees
                    .FirstOrDefaultAsync(e => e.Email == userAccount.Email && !e.NIC.StartsWith("DUTY") && e.NIC != "DUTY-ACC");
            }
            if (employee == null && !string.IsNullOrEmpty(username))
            {
                employee = await _db.Employees
                    .FirstOrDefaultAsync(e => e.Email == username && !e.NIC.StartsWith("DUTY") && e.NIC != "DUTY-ACC");
            }
            return employee;
        }

        public List<WelfareType> WelfareTypes { get; set; } = new();

        private async Task LoadWelfareTypesAsync()
        {
            WelfareTypes = await _db.WelfareTypes.Where(w => w.IsActive).OrderBy(w => w.WelfareTypeId).ToListAsync();
            if (!WelfareTypes.Any())
            {
                var defaultTypes = new[]
                {
                    new WelfareType { TypeName = "Medical Assistance", Category = "Health & Welfare", MaxEligibleAmount = 100000m, IsActive = true, CreatedAt = DateTime.Now },
                    new WelfareType { TypeName = "Education Assistance", Category = "Education", MaxEligibleAmount = 50000m, IsActive = true, CreatedAt = DateTime.Now },
                    new WelfareType { TypeName = "Housing Loan", Category = "Housing", MaxEligibleAmount = 500000m, IsActive = true, CreatedAt = DateTime.Now },
                    new WelfareType { TypeName = "Festival Advance", Category = "Financial", MaxEligibleAmount = 25000m, IsActive = true, CreatedAt = DateTime.Now },
                    new WelfareType { TypeName = "Funeral Assistance", Category = "Emergency", MaxEligibleAmount = 30000m, IsActive = true, CreatedAt = DateTime.Now }
                };
                _db.WelfareTypes.AddRange(defaultTypes);
                await _db.SaveChangesAsync();
                WelfareTypes = await _db.WelfareTypes.Where(w => w.IsActive).OrderBy(w => w.WelfareTypeId).ToListAsync();
            }
        }

        public int ApprovedThisYearCount { get; set; }
        public int CurrentYear => DateTime.Now.Year;
        public bool IsAnnualLimitReached => ApprovedThisYearCount >= 2;

        public async Task OnGetAsync()
        {
            await LoadCurrentUserAsync();
            await LoadWelfareTypesAsync();
            var emp = await GetCurrentEmployeeAsync();
            if (emp != null)
            {
                EmployeeId = emp.Id;
                var currentYear = DateTime.Now.Year;
                ApprovedThisYearCount = await _db.WelfareRequests.CountAsync(r =>
                    r.EmployeeId == emp.Id &&
                    (r.Status == "Approved" || r.CurrentStatus == "Approved" || r.Status == "PaymentCompleted") &&
                    (r.RequestDate.Year == currentYear || r.CreatedAt.Year == currentYear));
            }
        }

        public async Task<IActionResult> OnPostAsync()
        {
            await LoadCurrentUserAsync();
            var emp = await GetCurrentEmployeeAsync();
            if (emp != null)
            {
                EmployeeId = emp.Id;
            }

            if (EmployeeId <= 0)
            {
                await LoadWelfareTypesAsync();
                TempData["Error"] = "Unable to identify your employee account. Please ensure your login user is linked to an active employee profile in the system.";
                return Page();
            }

            var currentYear = DateTime.Now.Year;
            var approvedCount = await _db.WelfareRequests.CountAsync(r =>
                r.EmployeeId == EmployeeId &&
                (r.Status == "Approved" || r.CurrentStatus == "Approved" || r.Status == "PaymentCompleted") &&
                (r.RequestDate.Year == currentYear || r.CreatedAt.Year == currentYear));

            ApprovedThisYearCount = approvedCount;

            if (approvedCount >= 2)
            {
                await LoadWelfareTypesAsync();
                TempData["Error"] = $"You have already reached the maximum annual limit of 2 approved welfare requests for {currentYear}. You cannot submit further applications for this year.";
                return Page();
            }

            try
            {
                var reqDate = DateTime.TryParse(RequestDate, out var d) ? d : DateTime.Today;

                if (reqDate.Date < DateTime.Today)
                {
                    await LoadWelfareTypesAsync();
                    TempData["Error"] = "Request date cannot be in the past. Please select today or a future date.";
                    return Page();
                }

                var selectedType = WelfareTypes.FirstOrDefault(w => w.WelfareTypeId == WelfareTypeId);
                if (selectedType == null)
                {
                    await LoadWelfareTypesAsync();
                    selectedType = WelfareTypes.FirstOrDefault(w => w.WelfareTypeId == WelfareTypeId);
                }

                if (selectedType != null && RequestedAmount > selectedType.MaxEligibleAmount)
                {
                    await LoadWelfareTypesAsync();
                    TempData["Error"] = $"Requested amount exceeds the maximum eligible limit of LKR {selectedType.MaxEligibleAmount:N2} for {selectedType.TypeName}.";
                    return Page();
                }

                var request = new WelfareRequest
                {
                    EmployeeId = EmployeeId,
                    WelfareTypeId = WelfareTypeId,
                    RequestDate = reqDate,
                    RequestedAmount = RequestedAmount,
                    Remark = Remark,
                    IsDraft = IsDraft,
                    Status = IsDraft ? "Draft" : "Pending",
                    CurrentLevel = "DepartmentHead",
                    CurrentStatus = "Pending",
                    CreatedAt = DateTime.Now,
                    SubmittedBy = EmployeeId
                };

                _db.WelfareRequests.Add(request);
                await _db.SaveChangesAsync();

                if (Documents != null && Documents.Count > 0)
                {
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

                        _db.WelfareDocuments.Add(new WelfareDocument
                        {
                            RequestId = request.RequestId,
                            FileName = file.FileName,
                            FilePath = $"/uploads/welfare/{request.RequestId}/{uniqueName}",
                            FileType = file.ContentType,
                            UploadedAt = DateTime.Now
                        });
                    }

                    await _db.SaveChangesAsync();
                }

                if (!IsDraft)
                {
                    var applicant = await _db.Employees.FindAsync(EmployeeId);
                    var headOfWelfareUsers = (await _userManager.GetUsersInRoleAsync("Welfare Manager")).ToList();
                    if (!headOfWelfareUsers.Any())
                    {
                        headOfWelfareUsers = await _db.Users
                            .Where(u => u.UserName == "head.welfare" || u.Email == "head.welfare@kanrich.lk" || u.Department == "Welfare")
                            .ToListAsync();
                    }
                    if (!headOfWelfareUsers.Any())
                    {
                        var dhUsers = await _userManager.GetUsersInRoleAsync("Department Head");
                        headOfWelfareUsers = dhUsers.Where(u => u.Department == "Welfare" || u.UserName == "head.welfare").ToList();
                    }

                    foreach (var hw in headOfWelfareUsers)
                    {
                        if (!string.IsNullOrEmpty(hw.Email))
                        {
                            try
                            {
                                await _notifService.CreateNotificationAsync(
                                    hw.Email,
                                    "New Welfare Request Pending Approval",
                                    $"A new welfare request (WF-{request.RequestId:D4}) of LKR {request.RequestedAmount:N2} from {applicant?.FullName ?? "an employee"} is pending your approval.",
                                    CoreNotificationType.Info,
                                    "/Welfare/Approvals/DepartmentHeadApproval"
                                );
                            }
                            catch { }
                        }
                    }
                }

                TempData["Success"] = IsDraft
                    ? "Draft saved successfully!"
                    : "Your welfare request has been submitted to the Welfare Manager for approval.";

                return RedirectToPage("/Welfare/RequestList");
            }
            catch (Exception ex)
            {
                await LoadWelfareTypesAsync();
                var errorDetails = ex.InnerException != null ? $"{ex.Message} ({ex.InnerException.Message})" : ex.Message;
                TempData["Error"] = "An error occurred: " + errorDetails;
                return Page();
            }
        }
    }
}
