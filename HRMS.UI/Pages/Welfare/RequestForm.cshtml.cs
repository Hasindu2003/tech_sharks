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
using System.Text.RegularExpressions;
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

        [BindProperty(SupportsGet = true)] public int? RequestId { get; set; }
        [BindProperty] public int EmployeeId { get; set; }
        [BindProperty] public int WelfareTypeId { get; set; }
        [BindProperty] public string RequestDate { get; set; } = string.Empty;
        [BindProperty] public decimal RequestedAmount { get; set; }
        [BindProperty] public string Remark { get; set; } = string.Empty;
        [BindProperty] public string Urgency { get; set; } = "Normal";
        [BindProperty] public int? RepaymentMonths { get; set; }
        [BindProperty] public bool IsDraft { get; set; }
        [BindProperty] public List<IFormFile>? Documents { get; set; }

        public List<WelfareDocument> ExistingDocuments { get; set; } = new();

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

        public async Task OnGetAsync(int? id)
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

                if (id.HasValue && id.Value > 0)
                {
                    var existingDraft = await _db.WelfareRequests
                        .Include(r => r.Documents)
                        .FirstOrDefaultAsync(r => r.RequestId == id.Value && r.EmployeeId == emp.Id);

                    if (existingDraft != null && (existingDraft.IsDraft || existingDraft.Status == "Draft" || existingDraft.CurrentStatus == "Draft"))
                    {
                        RequestId = existingDraft.RequestId;
                        WelfareTypeId = existingDraft.WelfareTypeId;
                        RequestDate = existingDraft.RequestDate.ToString("yyyy-MM-dd");
                        RequestedAmount = existingDraft.RequestedAmount;

                        var rawRemark = existingDraft.Remark ?? "";

                        var urgencyMatch = Regex.Match(rawRemark, @"\[Urgency:\s*(High|Medium|Normal)\]", RegexOptions.IgnoreCase);
                        if (urgencyMatch.Success)
                        {
                            Urgency = urgencyMatch.Groups[1].Value switch
                            {
                                var s when s.Equals("High", StringComparison.OrdinalIgnoreCase) => "High",
                                var s when s.Equals("Medium", StringComparison.OrdinalIgnoreCase) => "Medium",
                                _ => "Normal"
                            };
                            rawRemark = rawRemark.Replace(urgencyMatch.Value, "").Trim();
                        }
                        else
                        {
                            Urgency = WelfarePayrollHelper.GetUrgency(rawRemark);
                        }

                        var repaymentMatch = Regex.Match(rawRemark, @"\(Repayment:\s*(\d+)\s*months\)", RegexOptions.IgnoreCase);
                        if (repaymentMatch.Success && int.TryParse(repaymentMatch.Groups[1].Value, out int months))
                        {
                            RepaymentMonths = months;
                            Remark = rawRemark.Replace(repaymentMatch.Value, "").Trim();
                        }
                        else if (rawRemark.StartsWith("Repayment:", StringComparison.OrdinalIgnoreCase))
                        {
                            var mMatch = Regex.Match(rawRemark, @"Repayment:\s*(\d+)\s*months", RegexOptions.IgnoreCase);
                            if (mMatch.Success && int.TryParse(mMatch.Groups[1].Value, out int m))
                            {
                                RepaymentMonths = m;
                                Remark = "";
                            }
                            else
                            {
                                Remark = rawRemark;
                            }
                        }
                        else
                        {
                            Remark = rawRemark;
                        }

                        ExistingDocuments = existingDraft.Documents.ToList();
                    }
                }
            }
        }

        public async Task<IActionResult> OnPostDeleteDocumentAsync(int docId, int reqId)
        {
            await LoadCurrentUserAsync();
            var emp = await GetCurrentEmployeeAsync();
            if (emp == null) return Forbid();

            var req = await _db.WelfareRequests.FirstOrDefaultAsync(r => r.RequestId == reqId && r.EmployeeId == emp.Id);
            if (req == null || (!req.IsDraft && req.Status != "Draft" && req.CurrentStatus != "Draft")) return Forbid();

            var doc = await _db.WelfareDocuments.FirstOrDefaultAsync(d => d.DocumentId == docId && d.RequestId == reqId);
            if (doc != null)
            {
                try
                {
                    var webRoot = _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot");
                    var filePath = Path.Combine(webRoot, doc.FilePath.TrimStart('/'));
                    if (System.IO.File.Exists(filePath))
                    {
                        System.IO.File.Delete(filePath);
                    }
                }
                catch { }

                _db.WelfareDocuments.Remove(doc);
                await _db.SaveChangesAsync();
                TempData["Success"] = "Document removed.";
            }

            return RedirectToPage("/Welfare/RequestForm", new { id = reqId });
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

            if (approvedCount >= 2 && !IsDraft)
            {
                await LoadWelfareTypesAsync();
                TempData["Error"] = $"You have already reached the maximum annual limit of 2 approved welfare requests for {currentYear}. You cannot submit further applications for this year.";
                return Page();
            }

            try
            {
                await LoadWelfareTypesAsync();

                var selectedType = WelfareTypes.FirstOrDefault(w => w.WelfareTypeId == WelfareTypeId);
                if (WelfareTypeId <= 0 || selectedType == null)
                {
                    if (IsDraft)
                    {
                        if (WelfareTypes.Any())
                        {
                            selectedType = WelfareTypes.First();
                            WelfareTypeId = selectedType.WelfareTypeId;
                        }
                        else
                        {
                            TempData["Error"] = "Please select an Assistance Type before saving as a draft.";
                            return Page();
                        }
                    }
                    else
                    {
                        TempData["Error"] = "Please select a valid Assistance Type.";
                        return Page();
                    }
                }

                var reqDate = DateTime.TryParse(RequestDate, out var d) ? d : DateTime.Today;

                if (!IsDraft && reqDate.Date < DateTime.Today)
                {
                    TempData["Error"] = "Request date cannot be in the past. Please select today or a future date.";
                    return Page();
                }
                else if (IsDraft && reqDate == default)
                {
                    reqDate = DateTime.Today;
                }

                if (!IsDraft)
                {
                    if (RequestedAmount <= 0)
                    {
                        TempData["Error"] = "Please enter a valid requested amount.";
                        return Page();
                    }

                    if (string.IsNullOrWhiteSpace(Remark) || Remark.Trim().Length < 5)
                    {
                        TempData["Error"] = "Please provide a clear reason for your request (at least 5 characters).";
                        return Page();
                    }
                }

                if (selectedType != null && RequestedAmount > selectedType.MaxEligibleAmount)
                {
                    TempData["Error"] = $"Requested amount exceeds the maximum eligible limit of LKR {selectedType.MaxEligibleAmount:N2} for {selectedType.TypeName}.";
                    return Page();
                }

                var finalRemark = Remark?.Trim() ?? string.Empty;
                if (RepaymentMonths.HasValue && RepaymentMonths.Value > 0)
                {
                    finalRemark = string.IsNullOrWhiteSpace(finalRemark) 
                        ? $"Repayment: {RepaymentMonths.Value} months" 
                        : $"{finalRemark} (Repayment: {RepaymentMonths.Value} months)";
                }

                var urgencyVal = string.IsNullOrWhiteSpace(Urgency) ? "Normal" : Urgency.Trim();
                if (!finalRemark.Contains("[Urgency:", StringComparison.OrdinalIgnoreCase))
                {
                    finalRemark = $"[Urgency: {urgencyVal}] {finalRemark}".Trim();
                }

                WelfareRequest request;
                bool isExisting = false;

                if (RequestId.HasValue && RequestId.Value > 0)
                {
                    var existing = await _db.WelfareRequests
                        .Include(r => r.Documents)
                        .FirstOrDefaultAsync(r => r.RequestId == RequestId.Value && r.EmployeeId == EmployeeId);

                    if (existing != null && (existing.IsDraft || existing.Status == "Draft" || existing.CurrentStatus == "Draft"))
                    {
                        request = existing;
                        isExisting = true;
                    }
                    else
                    {
                        request = new WelfareRequest();
                    }
                }
                else
                {
                    request = new WelfareRequest();
                }

                request.EmployeeId = EmployeeId;
                request.WelfareTypeId = WelfareTypeId;
                request.RequestDate = reqDate;
                request.RequestedAmount = RequestedAmount;
                request.Remark = finalRemark;
                request.IsDraft = IsDraft;
                request.Status = IsDraft ? "Draft" : "Pending";
                request.CurrentLevel = IsDraft ? "Draft" : "DepartmentHead";
                request.CurrentStatus = IsDraft ? "Draft" : "Pending";
                request.SubmittedBy = EmployeeId;

                if (!isExisting)
                {
                    request.CreatedAt = DateTime.Now;
                    _db.WelfareRequests.Add(request);
                }
                else if (!IsDraft)
                {
                    request.CreatedAt = DateTime.Now;
                }

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
                    ? $"Draft saved successfully (WF-{request.RequestId:D4})! You can edit or submit it anytime from My Requests."
                    : $"Welfare request WF-{request.RequestId:D4} has been submitted successfully.";

                if (User.IsInRole("Welfare Manager"))
                {
                    return RedirectToPage("/Welfare/Approvals/DepartmentHeadApproval");
                }

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
