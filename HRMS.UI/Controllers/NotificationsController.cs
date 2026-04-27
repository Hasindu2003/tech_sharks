using HRMS.Infrastructure.Identity;
using HRMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HRMS.UI.Controllers
{
    [Authorize]
    [Route("api/notifications")]
    [ApiController]
    public class NotificationsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public NotificationsController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        [HttpGet]
        public async Task<IActionResult> GetNotifications()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var notifications = new List<NotificationDto>();

            // ── Employee notifications ─────────────────────────────────────────
            if (User.IsInRole("Employee"))
            {
                var employee = await _context.Employees
                    .FirstOrDefaultAsync(e => e.Email == user.Email);

                if (employee != null)
                {
                    // Requests that got approved
                    var approvedRequests = await _context.WelfareRequests
                        .Include(r => r.WelfareType)
                        .Where(r => r.EmployeeId == employee.Id
                                 && r.Status == "Approved"
                                 && !r.IsDraft)
                        .OrderByDescending(r => r.CreatedAt)
                        .Take(5)
                        .ToListAsync();

                    foreach (var req in approvedRequests)
                    {
                        notifications.Add(new NotificationDto
                        {
                            Id = req.RequestId,
                            Title = "Request Approved",
                            Message = $"Your {req.WelfareType?.TypeName} request (WF-{req.RequestId:D4}) has been approved.",
                            Type = "success",
                            Icon = "check_circle",
                            Time = req.CreatedAt,
                            Link = $"/Welfare/StatusTracking?id={req.RequestId}"
                        });
                    }

                    // Requests that got rejected
                    var rejectedRequests = await _context.WelfareRequests
                        .Include(r => r.WelfareType)
                        .Where(r => r.EmployeeId == employee.Id
                                 && r.Status == "Rejected"
                                 && !r.IsDraft)
                        .OrderByDescending(r => r.CreatedAt)
                        .Take(5)
                        .ToListAsync();

                    foreach (var req in rejectedRequests)
                    {
                        notifications.Add(new NotificationDto
                        {
                            Id = req.RequestId,
                            Title = "Request Rejected",
                            Message = $"Your {req.WelfareType?.TypeName} request (WF-{req.RequestId:D4}) was rejected.",
                            Type = "error",
                            Icon = "cancel",
                            Time = req.CreatedAt,
                            Link = $"/Welfare/StatusTracking?id={req.RequestId}"
                        });
                    }

                    // Requests under review
                    var underReviewRequests = await _context.WelfareRequests
                        .Include(r => r.WelfareType)
                        .Where(r => r.EmployeeId == employee.Id
                                 && r.Status == "UnderReview"
                                 && !r.IsDraft)
                        .OrderByDescending(r => r.CreatedAt)
                        .Take(3)
                        .ToListAsync();

                    foreach (var req in underReviewRequests)
                    {
                        notifications.Add(new NotificationDto
                        {
                            Id = req.RequestId,
                            Title = "Request Under Review",
                            Message = $"Your {req.WelfareType?.TypeName} request (WF-{req.RequestId:D4}) is being reviewed by {req.CurrentLevel}.",
                            Type = "info",
                            Icon = "hourglass_top",
                            Time = req.CreatedAt,
                            Link = $"/Welfare/StatusTracking?id={req.RequestId}"
                        });
                    }

                    // Pending requests
                    var pendingRequests = await _context.WelfareRequests
                        .Include(r => r.WelfareType)
                        .Where(r => r.EmployeeId == employee.Id
                                 && r.CurrentStatus == "Pending"
                                 && !r.IsDraft)
                        .OrderByDescending(r => r.CreatedAt)
                        .Take(3)
                        .ToListAsync();

                    foreach (var req in pendingRequests)
                    {
                        notifications.Add(new NotificationDto
                        {
                            Id = req.RequestId,
                            Title = "Request Pending",
                            Message = $"Your {req.WelfareType?.TypeName} request (WF-{req.RequestId:D4}) is pending at {req.CurrentLevel}.",
                            Type = "warning",
                            Icon = "pending_actions",
                            Time = req.CreatedAt,
                            Link = $"/Welfare/StatusTracking?id={req.RequestId}"
                        });
                    }
                }
            }

            // ── BranchDGM notifications ────────────────────────────────────────
            if (User.IsInRole("BranchDGM"))
            {
                var pending = await _context.WelfareRequests
                    .Include(r => r.WelfareType)
                    .Include(r => r.Employee)
                    .Where(r => r.CurrentLevel == "BranchDGM" && r.CurrentStatus == "Pending")
                    .OrderByDescending(r => r.CreatedAt)
                    .Take(5)
                    .ToListAsync();

                foreach (var req in pending)
                {
                    notifications.Add(new NotificationDto
                    {
                        Id = req.RequestId,
                        Title = "Approval Required",
                        Message = $"{req.Employee?.FirstName} {req.Employee?.LastName} submitted a {req.WelfareType?.TypeName} request (WF-{req.RequestId:D4}) waiting your approval.",
                        Type = "warning",
                        Icon = "approval",
                        Time = req.CreatedAt,
                        Link = "/Welfare/Approvals/BranchDGMApproval"
                    });
                }

                if (pending.Count == 0)
                {
                    notifications.Add(new NotificationDto
                    {
                        Id = 0,
                        Title = "All Clear",
                        Message = "No pending requests for your approval.",
                        Type = "success",
                        Icon = "check_circle",
                        Time = DateTime.Now,
                        Link = "/Welfare/Approvals/BranchDGMApproval"
                    });
                }
            }

            // ── HODGM notifications ────────────────────────────────────────────
            if (User.IsInRole("HODGM"))
            {
                var pending = await _context.WelfareRequests
                    .Include(r => r.WelfareType)
                    .Include(r => r.Employee)
                    .Where(r => r.CurrentLevel == "HODGM" && r.CurrentStatus == "Pending")
                    .OrderByDescending(r => r.CreatedAt)
                    .Take(5)
                    .ToListAsync();

                foreach (var req in pending)
                {
                    notifications.Add(new NotificationDto
                    {
                        Id = req.RequestId,
                        Title = "Approval Required",
                        Message = $"{req.Employee?.FirstName} {req.Employee?.LastName}'s {req.WelfareType?.TypeName} request (WF-{req.RequestId:D4}) needs your review.",
                        Type = "warning",
                        Icon = "approval",
                        Time = req.CreatedAt,
                        Link = "/Welfare/Approvals/HODGMApproval"
                    });
                }
            }

            // ── SeniorManagement notifications ────────────────────────────────
            if (User.IsInRole("SeniorManagement"))
            {
                var pending = await _context.WelfareRequests
                    .Include(r => r.WelfareType)
                    .Include(r => r.Employee)
                    .Where(r => r.CurrentLevel == "SeniorManagement" && r.CurrentStatus == "Pending")
                    .OrderByDescending(r => r.CreatedAt)
                    .Take(5)
                    .ToListAsync();

                foreach (var req in pending)
                {
                    notifications.Add(new NotificationDto
                    {
                        Id = req.RequestId,
                        Title = "Final Approval Required",
                        Message = $"{req.Employee?.FirstName} {req.Employee?.LastName}'s {req.WelfareType?.TypeName} request (WF-{req.RequestId:D4}) needs your final approval.",
                        Type = "warning",
                        Icon = "approval",
                        Time = req.CreatedAt,
                        Link = "/Welfare/Approvals/SeniorManagementApproval"
                    });
                }
            }

            // ── Finance notifications ──────────────────────────────────────────
            if (User.IsInRole("Finance"))
            {
                var pending = await _context.WelfareRequests
                    .Include(r => r.WelfareType)
                    .Include(r => r.Employee)
                    .Where(r => r.CurrentLevel == "Finance" && r.CurrentStatus == "Pending")
                    .OrderByDescending(r => r.CreatedAt)
                    .Take(5)
                    .ToListAsync();

                foreach (var req in pending)
                {
                    notifications.Add(new NotificationDto
                    {
                        Id = req.RequestId,
                        Title = "Payment Required",
                        Message = $"Process payment for {req.Employee?.FirstName} {req.Employee?.LastName}'s {req.WelfareType?.TypeName} request (WF-{req.RequestId:D4}).",
                        Type = "warning",
                        Icon = "payments",
                        Time = req.CreatedAt,
                        Link = "/Welfare/Approvals/FinanceApproval"
                    });
                }
            }

            // ── Admin notifications ────────────────────────────────────────────
            if (User.IsInRole("Admin"))
            {
                var totalPending = await _context.WelfareRequests
                    .CountAsync(r => r.CurrentStatus == "Pending" && !r.IsDraft);

                if (totalPending > 0)
                {
                    notifications.Add(new NotificationDto
                    {
                        Id = 0,
                        Title = "Pending Requests",
                        Message = $"There are {totalPending} welfare request(s) pending approval across all levels.",
                        Type = "warning",
                        Icon = "pending_actions",
                        Time = DateTime.Now,
                        Link = "/Welfare/RequestList"
                    });
                }

                var recentRequests = await _context.WelfareRequests
                    .Include(r => r.WelfareType)
                    .Include(r => r.Employee)
                    .Where(r => !r.IsDraft)
                    .OrderByDescending(r => r.CreatedAt)
                    .Take(5)
                    .ToListAsync();

                foreach (var req in recentRequests)
                {
                    notifications.Add(new NotificationDto
                    {
                        Id = req.RequestId,
                        Title = "New Request",
                        Message = $"{req.Employee?.FirstName} {req.Employee?.LastName} submitted a {req.WelfareType?.TypeName} request (WF-{req.RequestId:D4}).",
                        Type = "info",
                        Icon = "description",
                        Time = req.CreatedAt,
                        Link = $"/Welfare/StatusTracking?id={req.RequestId}"
                    });
                }
            }

            // Sort by time descending and return top 10
            var result = notifications
                .OrderByDescending(n => n.Time)
                .Take(10)
                .ToList();

            return Ok(result);
        }
    }

    public class NotificationDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";
        public string Message { get; set; } = "";
        public string Type { get; set; } = "info"; // success, error, warning, info
        public string Icon { get; set; } = "notifications";
        public DateTime Time { get; set; }
        public string Link { get; set; } = "#";
        public string TimeAgo => GetTimeAgo(Time);

        private static string GetTimeAgo(DateTime time)
        {
            var diff = DateTime.Now - time;
            if (diff.TotalMinutes < 1) return "Just now";
            if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes}m ago";
            if (diff.TotalHours < 24) return $"{(int)diff.TotalHours}h ago";
            if (diff.TotalDays < 7) return $"{(int)diff.TotalDays}d ago";
            return time.ToString("MMM dd");
        }
    }
}
