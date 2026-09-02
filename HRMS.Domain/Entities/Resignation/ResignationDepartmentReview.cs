using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HRMS.Domain.Entities.Resignation
{
    public class ResignationDepartmentReview
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int ResignationRequestId { get; set; }

        [ForeignKey("ResignationRequestId")]
        public ResignationRequest ResignationRequest { get; set; } = null!;

        public int? DepartmentId { get; set; }

        [Required]
        [MaxLength(100)]
        public string DepartmentName { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? ReviewerUserId { get; set; }

        [MaxLength(150)]
        public string? ReviewerName { get; set; }

        [MaxLength(256)]
        public string? ReviewerEmail { get; set; }

        /// <summary>
        /// "Pending", "Approved", or "Rejected"
        /// </summary>
        [Required]
        [MaxLength(50)]
        public string Status { get; set; } = "Pending";

        [MaxLength(1000)]
        public string? Comments { get; set; }

        public DateTime? ReviewDate { get; set; }
    }
}
