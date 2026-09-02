using System;

namespace HRMS.Domain.Entities.Core
{
    public class Employee
    {
        public int Id { get; set; }   // Primary Key

        // Basic Information
        public string FullName { get; set; } = null!;
        public string Initials { get; set; } = string.Empty;
        public string Sex { get; set; } = string.Empty;
        public string EmployeeType { get; set; } = string.Empty;

        public string NIC { get; set; } = null!;
        public DateTime DateOfBirth { get; set; }
        public DateTime? DateJoined { get; set; }

        public string Email { get; set; } = null!;
        public string PhoneNumber { get; set; } = null!;
        public string ResidentialAddress { get; set; } = string.Empty;

        // Spouse Details
        public string? SpouseName { get; set; }
        public string? SpouseContactNo { get; set; }

        public string EPFNumber { get; set; } = null!;
        public string ETFNumber { get; set; } = string.Empty;
        public string? BankName { get; set; }
        public string BankAccountName { get; set; } = null!;
        public string BankAccountNumber { get; set; } = null!;

        // Designation FK and navigation
        public int? DesignationId { get; set; }
        public Designation? Designation { get; set; }
        public DateTime? DateConfirmed { get; set; }
        public int ProbationPeriodMonths { get; set; }
        public int InternPeriodMonths { get; set; }
        public decimal PreviousExperienceYears { get; set; }
        public string Status { get; set; } = null!;

        // Every employee belongs to a department
        public int? DepartmentId { get; set; }
        public Department? Department { get; set; }

        // Every employee belongs to a branch
        public int BranchId { get; set; }
        public Branch Branch { get; set; } = null!;

        // Reporting Officer (Self-referencing relationship)
        public int? ReportingOfficerId { get; set; }
        public Employee? ReportingOfficer { get; set; }

        // Computed Name with Initials
        public string NameWithInitials => FormatNameWithInitials(FullName, Initials);

        public static string FormatNameWithInitials(string? fullName, string? initials)
        {
            if (!string.IsNullOrWhiteSpace(initials))
            {
                return initials.Trim();
            }

            if (!string.IsNullOrWhiteSpace(fullName))
            {
                var parts = fullName.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 1)
                {
                    var initPart = string.Join(" ", parts.Take(parts.Length - 1).Select(p => p[0].ToString().ToUpper() + "."));
                    var lastName = parts[^1];
                    return $"{initPart} {lastName}";
                }
                return parts[0];
            }

            return "Unknown";
        }
    }
}
