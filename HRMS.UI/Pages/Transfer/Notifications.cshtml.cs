using HRMS.Domain.Entities.Core;
using HRMS.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HRMS.UI.Pages.Transfer
{
    [Authorize(Roles = "HR Manager,Area Manager,Branch Manager,Employee")]
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
            var email = User.Identity?.Name;
            if (string.IsNullOrEmpty(email)) return;

            Notifications = await _notificationService.GetNotificationsAsync(email);
            UnreadCount = await _notificationService.GetUnreadCountAsync(email);
        }

        public async Task<IActionResult> OnPostMarkReadAsync(int id)
        {
            await _notificationService.MarkAsReadAsync(id);
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostMarkAllReadAsync()
        {
            var email = User.Identity?.Name;
            if (string.IsNullOrEmpty(email)) return RedirectToPage();

            await _notificationService.MarkAllAsReadAsync(email);
            return RedirectToPage();
        }
    }
}
