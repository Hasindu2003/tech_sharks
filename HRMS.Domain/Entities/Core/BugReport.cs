using System;

namespace HRMS.Domain.Entities.Core
{
    public class BugReport
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Severity { get; set; } = "Medium"; // Low, Medium, High, Critical
        public string Category { get; set; } = "UI/UX"; // UI/UX, Functionality, Data/Calculation, Performance, Other
        public string Status { get; set; } = "Open"; // Open, In Progress, Resolved, Closed
        
        // Auto-captured environment metadata
        public string PageUrl { get; set; } = string.Empty;
        public string? ReportedByUsername { get; set; }
        public string? ReportedByRole { get; set; }
        public string? ReportedByBranch { get; set; }
        public string? UserAgent { get; set; }
        public string? ScreenResolution { get; set; }
        public string? ConsoleErrors { get; set; }
        public string? ScreenshotPath { get; set; }
        public string? DeveloperNotes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ResolvedAt { get; set; }
    }
}
