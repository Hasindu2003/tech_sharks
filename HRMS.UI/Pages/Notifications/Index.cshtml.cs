using HRMS.Application.Notifications;
using HRMS.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HRMS.UI.Pages.Notifications
{
    [Authorize]
    public class IndexModel : PageModel
    {
        private readonly INotificationService _notificationService;
        private readonly UserManager<ApplicationUser> _userManager;

        public IndexModel(INotificationService notificationService, UserManager<ApplicationUser> userManager)
        {
            _notificationService = notificationService;
            _userManager = userManager;
        }

        public bool HasEmployeeProfile { get; set; }
        public List<NotificationDto> Notifications { get; set; } = new();

        public async Task OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user?.EmployeeId == null)
            {
                HasEmployeeProfile = false;
                return;
            }

            HasEmployeeProfile = true;
            Notifications = await _notificationService.GetForEmployeeAsync(user.EmployeeId.Value, 50);
            await _notificationService.MarkAllAsReadAsync(user.EmployeeId.Value);
        }
    }
}
