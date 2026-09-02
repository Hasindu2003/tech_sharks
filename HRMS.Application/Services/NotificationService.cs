using HRMS.Domain.Entities.Core;
using HRMS.Domain.Common;
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
            if (string.IsNullOrWhiteSpace(recipientEmail)) return;

            var user = await _context.Users.FirstOrDefaultAsync(u => 
                u.UserName == recipientEmail || 
                u.Email == recipientEmail || 
                u.Id == recipientEmail);
            if (user == null) return;

            // Deduplication guard: prevent identical notifications created within 15 seconds
            var cutoff = SriLankaTime.Now.AddSeconds(-15);
            var duplicateExists = await _context.Notifications.AnyAsync(n => 
                n.UserId == user.Id && 
                n.Title == title && 
                n.Message == message && 
                n.CreatedAt >= cutoff);
            if (duplicateExists) return;

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
                CreatedAt = SriLankaTime.Now
            });

            await _context.SaveChangesAsync();
        }

        public async Task CreateNotificationAsync(string recipientEmail, string title, string message, CoreNotificationType type, string targetUrl)
        {
            if (string.IsNullOrWhiteSpace(recipientEmail)) return;

            var user = await _context.Users.FirstOrDefaultAsync(u => 
                u.UserName == recipientEmail || 
                u.Email == recipientEmail || 
                u.Id == recipientEmail);
            if (user == null) return;

            // Deduplication guard: prevent identical notifications created within 15 seconds
            var cutoff = SriLankaTime.Now.AddSeconds(-15);
            var duplicateExists = await _context.Notifications.AnyAsync(n => 
                n.UserId == user.Id && 
                n.Title == title && 
                n.Message == message && 
                n.CreatedAt >= cutoff);
            if (duplicateExists) return;

            _context.Notifications.Add(new Notification
            {
                UserId = user.Id,
                Title = title,
                Message = message,
                Type = type,
                TargetUrl = targetUrl,
                IsRead = false,
                CreatedAt = SriLankaTime.Now
            });

            await _context.SaveChangesAsync();
        }

        public async Task<List<Notification>> GetNotificationsAsync(string email)
        {
            if (string.IsNullOrWhiteSpace(email)) return new List<Notification>();

            var user = await _context.Users.FirstOrDefaultAsync(u => 
                u.UserName == email || 
                u.Email == email || 
                u.Id == email);
            if (user == null) return new List<Notification>();

            return await _context.Notifications
                .Where(n => n.UserId == user.Id)
                .OrderByDescending(n => n.CreatedAt)
                .ToListAsync();
        }

        public async Task<int> GetUnreadCountAsync(string email)
        {
            if (string.IsNullOrWhiteSpace(email)) return 0;

            var user = await _context.Users.FirstOrDefaultAsync(u => 
                u.UserName == email || 
                u.Email == email || 
                u.Id == email);
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
            if (string.IsNullOrWhiteSpace(email)) return;

            var user = await _context.Users.FirstOrDefaultAsync(u => 
                u.UserName == email || 
                u.Email == email || 
                u.Id == email);
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
