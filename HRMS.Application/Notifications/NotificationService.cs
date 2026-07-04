using HRMS.Domain.Entities.Notifications;
using HRMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Application.Notifications
{
    public class NotificationService : INotificationService
    {
        private readonly ApplicationDbContext _context;
        private readonly IEmailSender _emailSender;

        public NotificationService(ApplicationDbContext context, IEmailSender emailSender)
        {
            _context = context;
            _emailSender = emailSender;
        }

        public async Task NotifyAsync(int employeeId, string title, string message, string? link = null)
        {
            _context.Notifications.Add(new Notification
            {
                EmployeeId = employeeId,
                Title = title,
                Message = message,
                Link = link,
                CreatedAt = DateTime.Now
            });
            await _context.SaveChangesAsync();

            var email = await _context.Employees
                .Where(e => e.Id == employeeId)
                .Select(e => e.Email)
                .FirstOrDefaultAsync();

            if (!string.IsNullOrWhiteSpace(email))
                await _emailSender.SendAsync(email, title, message);
        }

        public async Task<List<NotificationDto>> GetForEmployeeAsync(int employeeId, int take = 20)
        {
            return await _context.Notifications
                .Where(n => n.EmployeeId == employeeId)
                .OrderByDescending(n => n.CreatedAt)
                .Take(take)
                .Select(n => new NotificationDto
                {
                    Id = n.Id,
                    Title = n.Title,
                    Message = n.Message,
                    Link = n.Link,
                    IsRead = n.IsRead,
                    CreatedAt = n.CreatedAt
                })
                .ToListAsync();
        }

        public Task<int> GetUnreadCountAsync(int employeeId) =>
            _context.Notifications.CountAsync(n => n.EmployeeId == employeeId && !n.IsRead);

        public async Task MarkAsReadAsync(int employeeId, int notificationId)
        {
            var notification = await _context.Notifications
                .FirstOrDefaultAsync(n => n.Id == notificationId && n.EmployeeId == employeeId);
            if (notification == null || notification.IsRead)
                return;

            notification.IsRead = true;
            await _context.SaveChangesAsync();
        }

        public async Task MarkAllAsReadAsync(int employeeId)
        {
            var unread = await _context.Notifications
                .Where(n => n.EmployeeId == employeeId && !n.IsRead)
                .ToListAsync();

            foreach (var n in unread)
                n.IsRead = true;

            await _context.SaveChangesAsync();
        }
    }
}
