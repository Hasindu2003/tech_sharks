using HRMS.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HRMS.UI.Pages.Api
{
    [Authorize]
    [IgnoreAntiforgeryToken]
    public class NotificationsModel : PageModel
    {
        private readonly INotificationService _notificationService;

        public NotificationsModel(INotificationService notificationService)
        {
            _notificationService = notificationService;
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var email = User.Identity?.Name;
            if (string.IsNullOrEmpty(email)) return Unauthorized();

            var notifications = await _notificationService.GetNotificationsAsync(email);
            var unreadCount = await _notificationService.GetUnreadCountAsync(email);

            return new JsonResult(new { unreadCount, items = notifications });
        }

        public async Task<IActionResult> OnPostMarkAsReadAsync(int id)
        {
            var email = User.Identity?.Name;
            if (string.IsNullOrEmpty(email)) return Unauthorized();

            await _notificationService.MarkAsReadAsync(id);
            return new OkResult();
        }

        public async Task<IActionResult> OnPostMarkAllAsReadAsync()
        {
            var email = User.Identity?.Name;
            if (string.IsNullOrEmpty(email)) return Unauthorized();

            await _notificationService.MarkAllAsReadAsync(email);
            return new OkResult();
        }
    }
}
