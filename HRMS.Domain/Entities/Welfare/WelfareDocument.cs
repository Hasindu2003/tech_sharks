using System.ComponentModel.DataAnnotations.Schema;

namespace HRMS.Domain.Entities.Welfare
{
    public class WelfareDocument
    {
        public int DocumentId { get; set; }

        [ForeignKey(nameof(WelfareRequest))]
        public int RequestId { get; set; }

        public string FileName { get; set; } = null!;
        public string FilePath { get; set; } = null!;
        public string? FileType { get; set; }
        public DateTime UploadedAt { get; set; }

        // Navigation
        public WelfareRequest WelfareRequest { get; set; } = null!;
    }
}
