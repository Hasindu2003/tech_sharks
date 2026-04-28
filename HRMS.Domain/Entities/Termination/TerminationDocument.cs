using System.ComponentModel.DataAnnotations;

namespace HRMS.Domain.Entities.Termination
{
    public class TerminationDocument
    {
        [Key]
        public int Id { get; set; }

        public int TerminationRequestId { get; set; }

        public TerminationDocumentType DocumentType { get; set; }

        [Required]
        [MaxLength(256)]
        public string FileName { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string ContentType { get; set; } = string.Empty;

        [Required]
        public byte[] DocumentData { get; set; } = Array.Empty<byte>();

        public DateTime UploadedDate { get; set; } = DateTime.Now;

        // Navigation
        public TerminationRequest TerminationRequest { get; set; } = null!;
    }

    public enum TerminationDocumentType
    {
        TerminationLetter = 0,
        DisciplinaryReport = 1,
        WarningLetter = 2,
        Other = 3
    }
}
