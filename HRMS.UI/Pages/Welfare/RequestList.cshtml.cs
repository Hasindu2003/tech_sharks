using HRMS.Domain.Entities.Welfare;
using HRMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
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
    public class RequestListModel : BasePageModel
    {
        private readonly IWebHostEnvironment _env;

        public RequestListModel(ApplicationDbContext context, IWebHostEnvironment env)
            : base(context)
        {
            _env = env;
        }

        public List<WelfareRequest> Requests { get; set; } = new();
        public int ApprovedThisYearCount { get; set; }
        public int CurrentYear => DateTime.Now.Year;
        public bool IsAnnualLimitReached => ApprovedThisYearCount >= 2;

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

        public async Task OnGetAsync()
        {
            await LoadCurrentUserAsync();

            var employee = await GetCurrentEmployeeAsync();
            if (employee == null)
            {
                Requests = new List<WelfareRequest>();
                return;
            }

            Requests = await _db.WelfareRequests
                .Include(r => r.WelfareType)
                .Include(r => r.Employee)
                .Where(r => r.EmployeeId == employee.Id)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            var currentYear = DateTime.Now.Year;
            ApprovedThisYearCount = Requests.Count(r =>
                (r.Status == "Approved" || r.CurrentStatus == "Approved" || r.Status == "PaymentCompleted") &&
                (r.RequestDate.Year == currentYear || r.CreatedAt.Year == currentYear));
        }

        public async Task<IActionResult> OnPostDeleteAsync(int id)
        {
            await LoadCurrentUserAsync();
            var employee = await GetCurrentEmployeeAsync();
            if (employee == null) return Forbid();

            var request = await _db.WelfareRequests
                .Include(r => r.Documents)
                .FirstOrDefaultAsync(r => r.RequestId == id);

            if (request == null) return NotFound();

            if (request.EmployeeId != employee.Id)
                return Forbid();

            // Validate 24-hour deletion rule
            bool canDelete = request.IsDraft || (
                request.CurrentLevel == "DepartmentHead"
                && request.CurrentStatus == "Pending"
                && DateTime.Now <= request.CreatedAt.AddHours(24)
            );

            if (!canDelete)
            {
                TempData["Error"] = "This welfare request can no longer be deleted. Deletions are only permitted within 24 hours of submission prior to manager review.";
                return RedirectToPage();
            }

            try
            {
                if (request.Documents.Any())
                {
                    var webRoot = _env?.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
                    var folderPath = Path.Combine(webRoot, "uploads", "welfare", request.RequestId.ToString());
                    if (Directory.Exists(folderPath))
                    {
                        Directory.Delete(folderPath, true);
                    }
                }
            }
            catch { }

            // Explicitly clean up any child approvals and documents
            var approvals = await _db.WelfareApprovals.Where(a => a.RequestId == request.RequestId).ToListAsync();
            if (approvals.Any())
            {
                _db.WelfareApprovals.RemoveRange(approvals);
            }

            if (request.Documents.Any())
            {
                _db.WelfareDocuments.RemoveRange(request.Documents);
            }

            _db.WelfareRequests.Remove(request);
            await _db.SaveChangesAsync();

            TempData["Success"] = $"Welfare request WF-{request.RequestId:D4} has been deleted successfully.";
            return RedirectToPage();
        }
    }
}
