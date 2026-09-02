using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using HRMS.Infrastructure.Identity;
using HRMS.Infrastructure.Persistence;
using HRMS.Domain.Common;
using System.Linq;
using System.Threading.Tasks;

namespace HRMS.UI.Pages.Api
{
    [Authorize]
    [IgnoreAntiforgeryToken] // Allow simple AJAX POSTs without token for marking read
    public class NotificationsModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public NotificationsModel(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        private async Task<string?> ResolveCurrentUserIdAsync()
        {
            var userId = _userManager.GetUserId(User);
            if (!string.IsNullOrEmpty(userId)) return userId;

            var name = User.Identity?.Name;
            if (!string.IsNullOrEmpty(name))
            {
                var user = await _userManager.FindByNameAsync(name) ?? await _userManager.FindByEmailAsync(name);
                return user?.Id;
            }
            return null;
        }

        // Return JSON list of unread notifications
        public async Task<IActionResult> OnGetAsync()
        {
            var userId = await ResolveCurrentUserIdAsync();
            if (string.IsNullOrEmpty(userId))
                return new JsonResult(new { unreadCount = 0, items = new object[0] });

            var rawNotifications = await _context.Notifications
                .Where(n => n.UserId == userId && !n.IsRead)
                .OrderByDescending(n => n.CreatedAt)
                .Take(15) // Limit to latest 15 to keep payload light
                .ToListAsync();

            var notifications = rawNotifications.Select(n => new
            {
                id = n.Id,
                title = n.Title,
                message = n.Message,
                targetUrl = n.TargetUrl,
                createdAt = SriLankaTime.Format(n.CreatedAt, "MMM dd, hh:mm tt")
            }).ToList();

            return new JsonResult(new { 
                unreadCount = notifications.Count, 
                items = notifications 
            });
        }

        // Mark a single notification as read
        public async Task<IActionResult> OnPostMarkAsReadAsync(int id)
        {
            var userId = await ResolveCurrentUserIdAsync();
            if (string.IsNullOrEmpty(userId)) return new UnauthorizedResult();

            var notif = await _context.Notifications.FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId);
            if (notif != null)
            {
                notif.IsRead = true;
                await _context.SaveChangesAsync();
                return new OkResult();
            }

            return new NotFoundResult();
        }

        // Mark all notifications as read
        public async Task<IActionResult> OnPostMarkAllAsReadAsync()
        {
            var userId = await ResolveCurrentUserIdAsync();
            if (string.IsNullOrEmpty(userId)) return new UnauthorizedResult();

            var unreadNotifs = await _context.Notifications
                .Where(n => n.UserId == userId && !n.IsRead)
                .ToListAsync();

            foreach(var notif in unreadNotifs)
            {
                notif.IsRead = true;
            }

            if (unreadNotifs.Any())
            {
                await _context.SaveChangesAsync();
            }

            return new OkResult();
        }
    }
}
