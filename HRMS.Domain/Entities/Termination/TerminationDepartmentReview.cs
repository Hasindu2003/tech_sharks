using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HRMS.Domain.Entities.Termination
{
    public class TerminationDepartmentReview
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int TerminationRequestId { get; set; }

        [ForeignKey("TerminationRequestId")]
        public TerminationRequest? TerminationRequest { get; set; }

        [Required]
        [MaxLength(100)]
        public string DepartmentName { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? ReviewerUserId { get; set; }

        [MaxLength(150)]
        public string? ReviewerName { get; set; }

        [MaxLength(256)]
        public string? ReviewerEmail { get; set; }

        [MaxLength(1000)]
        public string? Comments { get; set; }

        public DateTime? ReviewDate { get; set; }

        [Required]
        [MaxLength(50)]
        public string Status { get; set; } = "Pending"; // Pending, Approved, Rejected
    }
}
