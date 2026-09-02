using System;
using HRMS.Domain.Common;

namespace HRMS.Domain.Entities.Core
{
    public class Notification
    {
        public int Id { get; set; }

        // The Identity User ID of the recipient
        public string UserId { get; set; } = null!;

        public string Title { get; set; } = null!;
        public string Message { get; set; } = null!;
        
        // Where the user is navigated when clicking the notification
        public string TargetUrl { get; set; } = null!;

        public bool IsRead { get; set; } = false;

        public DateTime CreatedAt { get; set; } = SriLankaTime.Now;

        // --- Separation & Transfer Integration ---
        public CoreNotificationType? Type { get; set; }
        public int? TransferRequestId { get; set; }
    }

    public enum CoreNotificationType
    {
        Info = 0,
        Approved = 1,
        Rejected = 2
    }
}
