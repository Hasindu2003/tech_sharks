using System;

namespace HRMS.Domain.Entities.Core
{
    public class EmployeeDocument
    {
        public int Id { get; set; }

        public int EmployeeId { get; set; }
        public Employee Employee { get; set; } = null!;

        public string DocumentType { get; set; } = null!;
        public string FileName { get; set; } = null!;
        public string StoredFileName { get; set; } = null!;
        public string ContentType { get; set; } = null!;

        public DateTime UploadedAt { get; set; } = DateTime.Now;

        public string Status { get; set; } = "Pending";
        public DateTime? ReviewedAt { get; set; }
        public string? ReviewedByUserId { get; set; }
        public string? ReviewerNotes { get; set; }
    }
}
