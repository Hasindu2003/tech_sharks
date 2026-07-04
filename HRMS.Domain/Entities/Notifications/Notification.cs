using HRMS.Domain.Entities.Core;

namespace HRMS.Domain.Entities.Notifications
{
    public class Notification
    {
        public int Id { get; set; }

        public int EmployeeId { get; set; }
        public Employee Employee { get; set; } = null!;

        public string Title { get; set; } = null!;
        public string Message { get; set; } = null!;
        public string? Link { get; set; }

        public bool IsRead { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
