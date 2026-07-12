using System;

namespace HRMS.Domain.Entities.Core
{
    public class EmployeeDocument
    {
        public int Id { get; set; }

        // FK to Employee
        public int EmployeeId { get; set; }
        public Employee Employee { get; set; } = null!;

        // Document Info
        public string DocumentType { get; set; } = null!;   // e.g. NIC, Passport, Bank Slip
        public string FileName { get; set; } = null!;         // Original display name
        public string StoredFileName { get; set; } = null!;   // GUID-based name on disk
        public string ContentType { get; set; } = null!;      // MIME type

        // Audit
        public DateTime UploadedAt { get; set; } = DateTime.Now;

        // Review
        public string Status { get; set; } = "Pending";       // Pending | Approved | Rejected
        public DateTime? ReviewedAt { get; set; }
        public string? ReviewedByUserId { get; set; }          // Identity user id
        public string? ReviewerNotes { get; set; }
    }
}
