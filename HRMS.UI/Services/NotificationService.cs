using HRMS.Domain.Entities.Transfer;
using HRMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HRMS.UI.Services
{
    public interface INotificationService
    {
        Task CreateNotificationAsync(string recipientEmail, string title, string message, NotificationType type, int transferRequestId);
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

        public async Task CreateNotificationAsync(string recipientEmail, string title, string message, NotificationType type, int transferRequestId)
        {
            var notification = new Notification
            {
                RecipientEmail = recipientEmail,
                Title = title,
                Message = message,
                Type = type,
                TransferRequestId = transferRequestId,
                IsRead = false,
                CreatedAt = DateTime.Now
            };

            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();
        }

        public async Task<List<Notification>> GetNotificationsAsync(string email)
        {
            return await _context.Notifications
                .Where(n => n.RecipientEmail == email)
                .OrderByDescending(n => n.CreatedAt)
                .ToListAsync();
        }

        public async Task<int> GetUnreadCountAsync(string email)
        {
            return await _context.Notifications
                .CountAsync(n => n.RecipientEmail == email && !n.IsRead);
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
            var unread = await _context.Notifications
                .Where(n => n.RecipientEmail == email && !n.IsRead)
                .ToListAsync();

            foreach (var n in unread)
                n.IsRead = true;

            await _context.SaveChangesAsync();
        }
    }
}
