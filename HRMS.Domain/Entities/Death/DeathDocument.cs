using System;
using System.ComponentModel.DataAnnotations;

namespace HRMS.Domain.Entities.Death
{
    public class DeathDocument
    {
        [Key]
        public int Id { get; set; }

        public int DeathRequestId { get; set; }
        public DeathRequest DeathRequest { get; set; } = null!;

        [Required]
        [MaxLength(256)]
        public string FileName { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string ContentType { get; set; } = string.Empty;

        [Required]
        public byte[] Content { get; set; } = Array.Empty<byte>();

        [Required]
        [MaxLength(100)]
        public string DocumentType { get; set; } = string.Empty;

        public DateTime UploadedDate { get; set; }
    }
}
