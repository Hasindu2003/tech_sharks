using System;
using System.ComponentModel.DataAnnotations;

namespace HRMS.Domain.DTOs
{
    public class BiometricLogDto
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Employee ID is required")]
        [Range(1, int.MaxValue, ErrorMessage = "Employee ID must be greater than 0")]
        public int EmployeeId { get; set; }

        [Required(ErrorMessage = "Log date/time is required")]
        public DateTime LogDateTime { get; set; }

        [Required(ErrorMessage = "Device ID is required")]
        [StringLength(50, ErrorMessage = "Device ID cannot exceed 50 characters")]
        public string DeviceId { get; set; }

        [Required(ErrorMessage = "Log type is required")]
        public string LogType { get; set; }
    }
    
    public class BiometricLogResponseDto
    {
        public int Id { get; set; }
        public int EmployeeId { get; set; }
        public string EmployeeName { get; set; } = string.Empty;
        public DateTime LogDateTime { get; set; }
        public string DeviceId { get; set; } = string.Empty;
        public string? LogType { get; set; }
    }
}
