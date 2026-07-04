namespace HRMS.Application.Notifications
{
    public interface INotificationService
    {
        // Creates an in-app notification for the employee and also sends it by email.
        Task NotifyAsync(int employeeId, string title, string message, string? link = null);

        Task<List<NotificationDto>> GetForEmployeeAsync(int employeeId, int take = 20);
        Task<int> GetUnreadCountAsync(int employeeId);
        Task MarkAsReadAsync(int employeeId, int notificationId);
        Task MarkAllAsReadAsync(int employeeId);
    }

    public class NotificationDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = null!;
        public string Message { get; set; } = null!;
        public string? Link { get; set; }
        public bool IsRead { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
