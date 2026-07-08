using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using HRMS.Infrastructure.Identity;
using HRMS.Infrastructure.Persistence;
using HRMS.Application.Services;

namespace HRMS.UI.Pages
{
    [Authorize]
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ITransferRequestService _transferService;

        public IndexModel(ApplicationDbContext context, UserManager<ApplicationUser> userManager, ITransferRequestService transferService)
        {
            _context = context;
            _userManager = userManager;
            _transferService = transferService;
        }

        // Stats
        public int TotalEmployees { get; set; }
        public int OnLeaveToday { get; set; }
        public int PendingRequests { get; set; }
        public int OpenPositions { get; set; }

        // Greeting
        public string GreetingName { get; set; } = "User";
        public string GreetingTime { get; set; } = "Good Morning";

        // Pending Approvals (real data from transfer service)
        public List<PendingApprovalItem> PendingApprovals { get; set; } = new();

        // Upcoming Events
        public List<UpcomingEventItem> UpcomingEvents { get; set; } = new();

        public async Task OnGetAsync()
        {
            var currentUser = await _userManager.GetUserAsync(User);

            // Greeting
            var hour = DateTime.Now.Hour;
            GreetingTime = hour < 12 ? "Good Morning" : hour < 17 ? "Good Afternoon" : "Good Evening";

            var email = User.Identity?.Name;
            if (!string.IsNullOrEmpty(email))
            {
                var employee = await _context.Employees.FirstOrDefaultAsync(e => e.Email == email);
                GreetingName = employee?.FullName ?? currentUser?.FullName ?? email;
            }

            // Total employees (scoped by role)
            int? scopedBranchId = null;
            List<int>? amBranchIds = null;

            if (User.IsInRole("Branch Manager") && !string.IsNullOrWhiteSpace(currentUser?.Branch))
            {
                var branch = await _context.Branches.FirstOrDefaultAsync(b => b.Name == currentUser.Branch);
                scopedBranchId = branch?.Id;
            }
            else if (User.IsInRole("Area Manager") && !string.IsNullOrWhiteSpace(currentUser?.ManagedBranches))
            {
                amBranchIds = currentUser.ManagedBranches
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => int.TryParse(s.Trim(), out var id) ? id : 0)
                    .Where(id => id > 0).ToList();
            }

            var countQuery = _context.Employees.Where(e => e.Status != "Draft");
            if (scopedBranchId.HasValue)
                countQuery = countQuery.Where(e => e.BranchId == scopedBranchId.Value);
            else if (amBranchIds != null)
                countQuery = countQuery.Where(e => amBranchIds.Contains(e.BranchId));
            TotalEmployees = await countQuery.CountAsync();

            OnLeaveToday = 8;
            OpenPositions = 4;

            // Pending approvals from transfer service
            List<Application.Models.TransferRequestViewModel> pending = new();
            if (User.IsInRole("Area Manager"))
                pending = await _transferService.GetRequestsForAreaManagerAsync();
            else if (User.IsInRole("HR Manager"))
                pending = await _transferService.GetRequestsForHRManagerAsync(currentUser?.Branch ?? "");
            else if (User.IsInRole("Branch Manager"))
                pending = await _transferService.GetPendingRequestsForBranchManagerAsync(currentUser?.Branch ?? "");
            else if (User.IsInRole("Employee"))
                pending = await _transferService.GetRequestsByUserAsync(email ?? "");

            PendingRequests = pending.Count;

            PendingApprovals = pending.Take(5).Select(r => new PendingApprovalItem
            {
                EmployeeName = r.EmployeeName,
                RequestType = "Transfer Request",
                DateRange = r.RequestedDate.ToString("MMM dd, yyyy"),
                Status = "Pending",
                RequestId = r.Id
            }).ToList();

            UpcomingEvents = new List<UpcomingEventItem>
            {
                new() { Title = "Design Team Meeting",   Time = "10:00 AM - 11:30 AM", Month = "Aug", Day = "25", ThemeClass = "event-green"  },
                new() { Title = "Monthly Town Hall",     Time = "02:00 PM - 03:00 PM", Month = "Aug", Day = "28", ThemeClass = "event-orange" },
                new() { Title = "Labor Day Holiday",     Time = "All Day",              Month = "Sep", Day = "02", ThemeClass = "event-blue"   },
            };
        }

        public class PendingApprovalItem
        {
            public int RequestId { get; set; }
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
