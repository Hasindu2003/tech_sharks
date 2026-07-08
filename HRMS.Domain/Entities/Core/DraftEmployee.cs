using System;

namespace HRMS.Domain.Entities.Core
{
    public class DraftEmployee
    {
        public int Id { get; set; }

        public string? FullName { get; set; }
        public string? Initials { get; set; }
        public string? Sex { get; set; }
        public string? EmployeeType { get; set; }

        public string? NIC { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public DateTime? DateJoined { get; set; }

        public string? Email { get; set; }
        public string? PhoneNumber { get; set; }
        public string? ResidentialAddress { get; set; }

        public string? SpouseName { get; set; }
        public string? SpouseContactNo { get; set; }

        public string? EPFNumber { get; set; }
        public string? ETFNumber { get; set; }
        public string? BankAccountName { get; set; }
        public string? BankAccountNumber { get; set; }

        public int? DesignationId { get; set; }
        public Designation? Designation { get; set; }

        public DateTime? DateConfirmed { get; set; }
        public int? ProbationPeriodMonths { get; set; }
        public int? InternPeriodMonths { get; set; }
        public decimal? PreviousExperienceYears { get; set; }
        public string? Status { get; set; }

        public DateTime? LastUpdated { get; set; }

        public int? DepartmentId { get; set; }
        public Department? Department { get; set; }

        public int? BranchId { get; set; }
        public Branch? Branch { get; set; }
    }
}
