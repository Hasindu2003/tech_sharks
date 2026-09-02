using HRMS.Domain.Entities.Transfer;
using HRMS.Domain.Entities.Core;
using HRMS.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HRMS.UI.Pages.Transfer
{
    [Authorize]
    public class NotificationsModel : PageModel
    {
        private readonly INotificationService _notificationService;

        public NotificationsModel(INotificationService notificationService)
        {
            _notificationService = notificationService;
        }

        public List<Notification> Notifications { get; set; } = new();
        public int UnreadCount { get; set; }

        public async Task OnGetAsync()
        {
            var identifier = User.Identity?.Name ?? "";
            Notifications = await _notificationService.GetNotificationsAsync(identifier);
            UnreadCount = await _notificationService.GetUnreadCountAsync(identifier);
        }

        public async Task<IActionResult> OnPostMarkReadAsync(int id)
        {
            await _notificationService.MarkAsReadAsync(id);
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostMarkAllReadAsync()
        {
            var identifier = User.Identity?.Name ?? "";
            await _notificationService.MarkAllAsReadAsync(identifier);
            return RedirectToPage();
        }
    }
}
