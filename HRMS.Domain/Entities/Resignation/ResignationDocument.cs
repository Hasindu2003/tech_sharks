using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HRMS.Domain.Entities.Resignation
{
    public class ResignationDocument
    {
        [Key]
        public int Id { get; set; }

        public int ResignationRequestId { get; set; }

        [Required]
        [MaxLength(256)]
        public string FileName { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string ContentType { get; set; } = string.Empty;

        public byte[] DocumentData { get; set; } = Array.Empty<byte>();

        public DateTime UploadedDate { get; set; }

        // ── Navigation ──
        [ForeignKey(nameof(ResignationRequestId))]
        public ResignationRequest ResignationRequest { get; set; } = null!;
    }
}
