using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace HRMS.Infrastructure.Identity
{
    public class ApplicationUser : IdentityUser
    {
        [Required]
        [MaxLength(100)]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [MaxLength(20)]
        public string EpfNumber { get; set; } = string.Empty;

        [Required]
        [MaxLength(200)]
        public string Branch { get; set; } = string.Empty;

        [Required]
        [MaxLength(200)]
        public string Designation { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? Department { get; set; }

        public DateTime DateOfJoining { get; set; }
        // Link to the Employee record (optional)
        public int? EmployeeId { get; set; }
    }
}
