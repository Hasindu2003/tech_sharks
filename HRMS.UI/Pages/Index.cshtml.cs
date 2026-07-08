using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using HRMS.Infrastructure.Identity;
using HRMS.Infrastructure.Persistence;

namespace HRMS.UI.Pages
{
    [Authorize]
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public IndexModel(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // Stats
        public int TotalEmployees { get; set; }
        public int OnLeaveToday { get; set; }
        public int PendingRequests { get; set; }
        public int OpenPositions { get; set; }

        // Greeting
        public string GreetingName { get; set; } = "Admin";
        public string GreetingTime { get; set; } = "Good Morning";

        // Pending Approvals
        public List<PendingApprovalItem> PendingApprovals { get; set; } = new();

        // Upcoming Events
        public List<UpcomingEventItem> UpcomingEvents { get; set; } = new();

        public async Task OnGetAsync()
        {
            // Determine branch scope by role
            int? scopedBranchId = null;
            List<int>? amBranchIds = null;
            var currentUser = await _userManager.GetUserAsync(User);

            if (User.IsInRole("HR Manager") && currentUser?.EmployeeId != null)
            {
                var hrEmployee = await _context.Employees.FindAsync(currentUser.EmployeeId.Value);
                scopedBranchId = hrEmployee?.BranchId;
            }
            else if (User.IsInRole("Branch Manager") && !string.IsNullOrWhiteSpace(currentUser?.Branch))
            {
                var branch = await _context.Branches.FirstOrDefaultAsync(b => b.Name == currentUser.Branch);
                scopedBranchId = branch?.Id;
            }
            else if (User.IsInRole("Area Manager") && !string.IsNullOrWhiteSpace(currentUser?.ManagedBranches))
            {
                amBranchIds = currentUser.ManagedBranches
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => int.TryParse(s.Trim(), out var id) ? id : 0)
                    .Where(id => id > 0)
                    .ToList();
            }

            var countQuery = _context.Employees.Where(e => e.Status != "Draft" && e.NIC != "DUTY-ACC");
            if (scopedBranchId.HasValue)
                countQuery = countQuery.Where(e => e.BranchId == scopedBranchId.Value);
            else if (amBranchIds != null)
                countQuery = countQuery.Where(e => amBranchIds.Contains(e.BranchId));
            TotalEmployees = await countQuery.CountAsync();

            // Greeting based on time
            var hour = DateTime.Now.Hour;
            if (hour < 12) GreetingTime = "Good Morning";
            else if (hour < 17) GreetingTime = "Good Afternoon";
            else GreetingTime = "Good Evening";

            var email = User.Identity?.Name;
            if (!string.IsNullOrEmpty(email))
            {
                var employee = await _context.Employees.FirstOrDefaultAsync(e => e.Email == email);
                GreetingName = employee?.FullName ?? email;
            }
            else
            {
                GreetingName = "Admin";
            }

            // Sample data for demo purposes (replace with real queries later)
            OnLeaveToday = 8;
            PendingRequests = 12;
            OpenPositions = 4;

            PendingApprovals = new List<PendingApprovalItem>
            {
                new() { EmployeeName = "Ruwan Deshapriya", RequestType = "Sick Leave", DateRange = "Aug 24 - Aug 25", Status = "Pending" },
                new() { EmployeeName = "Pabani Senarath", RequestType = "Expense Claim", DateRange = "Aug 22", Status = "Pending" },
                new() { EmployeeName = "Harshana Senevirathne", RequestType = "Vacation", DateRange = "Sep 01 - Sep 10", Status = "Pending" },
            };

            UpcomingEvents = new List<UpcomingEventItem>
            {
                new() { Title = "Design Team Meeting", Time = "10:00 AM - 11:30 AM", Month = "Aug", Day = "25", ThemeClass = "event-green" },
                new() { Title = "Monthly Town Hall", Time = "02:00 PM - 03:00 PM", Month = "Aug", Day = "28", ThemeClass = "event-orange" },
                new() { Title = "Labor Day Holiday", Time = "All Day", Month = "Sep", Day = "02", ThemeClass = "event-blue" },
            };
        }

        public class PendingApprovalItem
        {
            public string EmployeeName { get; set; } = "";
            public string RequestType { get; set; } = "";
            public string DateRange { get; set; } = "";
            public string Status { get; set; } = "";
        }

        public class UpcomingEventItem
        {
            public string Title { get; set; } = "";
            public string Time { get; set; } = "";
            public string Month { get; set; } = "";
            public string Day { get; set; } = "";
            public string ThemeClass { get; set; } = "";
        }
    }
}
