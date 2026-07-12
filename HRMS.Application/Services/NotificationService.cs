using HRMS.Domain.Entities.Core;
using HRMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Application.Services
{
    public interface INotificationService
    {
        Task CreateNotificationAsync(string recipientEmail, string title, string message, CoreNotificationType type, int transferRequestId, string targetUrl = "");
        Task CreateNotificationAsync(string recipientEmail, string title, string message, CoreNotificationType type, string targetUrl);
        Task<List<Notification>> GetNotificationsAsync(string email);
        Task<int> GetUnreadCountAsync(string email);
        Task MarkAsReadAsync(int id);
        Task MarkAllAsReadAsync(string email);
    }

    public class NotificationService : INotificationService
    {
        private readonly ApplicationDbContext _context;

        public NotificationService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task CreateNotificationAsync(string recipientEmail, string title, string message, CoreNotificationType type, int transferRequestId, string targetUrl = "")
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == recipientEmail);
            if (user == null) return;

            var resolvedUrl = string.IsNullOrEmpty(targetUrl)
                ? $"/Transfer/Details?id={transferRequestId}"
                : targetUrl;

            _context.Notifications.Add(new Notification
            {
                UserId = user.Id,
                Title = title,
                Message = message,
                Type = type,
                TransferRequestId = transferRequestId,
                TargetUrl = resolvedUrl,
                IsRead = false,
                CreatedAt = DateTime.Now
            });

            await _context.SaveChangesAsync();
        }

        public async Task CreateNotificationAsync(string recipientEmail, string title, string message, CoreNotificationType type, string targetUrl)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == recipientEmail);
            if (user == null) return;

            _context.Notifications.Add(new Notification
            {
                UserId = user.Id,
                Title = title,
                Message = message,
                Type = type,
                TargetUrl = targetUrl,
                IsRead = false,
                CreatedAt = DateTime.Now
            });

            await _context.SaveChangesAsync();
        }

        public async Task<List<Notification>> GetNotificationsAsync(string email)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (user == null) return new List<Notification>();

            return await _context.Notifications
                .Where(n => n.UserId == user.Id)
                .OrderByDescending(n => n.CreatedAt)
                .ToListAsync();
        }

        public async Task<int> GetUnreadCountAsync(string email)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (user == null) return 0;

            return await _context.Notifications
                .CountAsync(n => n.UserId == user.Id && !n.IsRead);
        }

        public async Task MarkAsReadAsync(int id)
        {
            var notification = await _context.Notifications.FindAsync(id);
            if (notification != null)
            {
                notification.IsRead = true;
                await _context.SaveChangesAsync();
            }
        }

        public async Task MarkAllAsReadAsync(string email)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (user == null) return;

            var unread = await _context.Notifications
                .Where(n => n.UserId == user.Id && !n.IsRead)
                .ToListAsync();

            foreach (var n in unread)
                n.IsRead = true;

            await _context.SaveChangesAsync();
        }
    }
}
