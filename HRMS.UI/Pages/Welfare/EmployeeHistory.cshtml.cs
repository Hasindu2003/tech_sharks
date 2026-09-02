using HRMS.Domain.Entities.Core;
using HRMS.Domain.Entities.Welfare;
using HRMS.Infrastructure.Identity;
using HRMS.Infrastructure.Persistence;
using HRMS.UI.Pages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HRMS.UI.Pages.Welfare
{
    using Employee = HRMS.Domain.Entities.Core.Employee;

    [Authorize(Roles = "Welfare Manager,Department Head,HR Manager,HR Officer,Area Manager,Branch Manager,Admin")]
    public class EmployeeHistoryModel : BasePageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;

        public EmployeeHistoryModel(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
            : base(context)
        {
            _userManager = userManager;
        }

        public Employee Employee { get; set; } = null!;
        public List<WelfareRequest> PastRequests { get; set; } = new();
        public decimal TotalDisbursedAmount { get; set; }
        public int TotalRequestsCount { get; set; }
        public int ApprovedRequestsCount { get; set; }
        public int RejectedRequestsCount { get; set; }
        public int PendingRequestsCount { get; set; }
        public string ServiceDuration { get; set; } = string.Empty;

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            await LoadCurrentUserAsync();

            if (!id.HasValue || id.Value <= 0)
            {
                return NotFound();
            }

            var emp = await _db.Employees
                .Include(e => e.Department)
                .Include(e => e.Designation)
                .Include(e => e.Branch)
                .Include(e => e.ReportingOfficer)
                .FirstOrDefaultAsync(e => e.Id == id.Value && !e.NIC.StartsWith("DUTY") && e.NIC != "DUTY-ACC");

            if (emp == null)
            {
                return NotFound();
            }

            Employee = emp;

            // Compute Service Duration
            if (emp.DateJoined.HasValue)
            {
                var now = DateTime.Today;
                var joined = emp.DateJoined.Value;
                int years = now.Year - joined.Year;
                int months = now.Month - joined.Month;
                if (now.Day < joined.Day) months--;
                if (months < 0) { years--; months += 12; }
                
                if (years > 0 && months > 0)
                    ServiceDuration = $"{years} yr{(years > 1 ? "s" : "")}, {months} mo{(months > 1 ? "s" : "")}";
                else if (years > 0)
                    ServiceDuration = $"{years} yr{(years > 1 ? "s" : "")}";
                else if (months > 0)
                    ServiceDuration = $"{months} mo{(months > 1 ? "s" : "")}";
                else
                    ServiceDuration = "Less than 1 month";
            }
            else
            {
                ServiceDuration = "N/A";
            }

            // Load all past welfare requests by this employee
            PastRequests = await _db.WelfareRequests
                .Include(r => r.WelfareType)
                .Include(r => r.Documents)
                .Where(r => r.EmployeeId == emp.Id)
                .OrderByDescending(r => r.RequestDate)
                .ThenByDescending(r => r.CreatedAt)
                .ToListAsync();

            TotalRequestsCount = PastRequests.Count;
            ApprovedRequestsCount = PastRequests.Count(r => r.CurrentStatus == "Approved" || r.CurrentStatus == "Disbursed" || r.Status == "Approved" || r.Status == "Disbursed");
            RejectedRequestsCount = PastRequests.Count(r => r.CurrentStatus == "Rejected" || r.Status == "Rejected");
            PendingRequestsCount = PastRequests.Count(r => r.CurrentStatus == "Pending" && r.Status != "Rejected");
            
            TotalDisbursedAmount = PastRequests
                .Where(r => r.CurrentStatus == "Approved" || r.CurrentStatus == "Disbursed" || r.Status == "Approved" || r.Status == "Disbursed")
                .Sum(r => r.ApprovedAmount ?? r.RequestedAmount);

            return Page();
        }
    }
}
