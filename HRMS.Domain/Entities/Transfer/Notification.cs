using System.ComponentModel.DataAnnotations;

namespace HRMS.Domain.Entities.Transfer
{
    public class Notification
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(256)]
        public string UserId { get; set; } = string.Empty;

        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        [Required]
        [MaxLength(1000)]
        public string Message { get; set; } = string.Empty;

        public NotificationType? Type { get; set; }

        public int? TransferRequestId { get; set; }

        [MaxLength(500)]
        public string TargetUrl { get; set; } = string.Empty;

        public bool IsRead { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }

    public enum NotificationType
    {
        Info = 0,
        Approved = 1,
        Rejected = 2
    }
}
