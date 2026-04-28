using System;

namespace HRMS.Domain.Entities.Core
{
    public class Employee
    {
        public int Id { get; set; }   // Primary Key

        // Basic Information
        public string FirstName { get; set; } = null!;
        public string? MiddleName { get; set; }
        public string LastName { get; set; } = null!;

        public string NIC { get; set; } = null!;
        public DateTime DateOfBirth { get; set; }
        public DateTime DateJoined { get; set; }

        public string Email { get; set; } = null!;
        public string PhoneNumber { get; set; } = null!;

        public string EPFNumber { get; set; } = null!;
        public string BankAccountName { get; set; } = null!;
        public string BankAccountNumber { get; set; } = null!;

        // Designation FK and navigation
        public int DesignationId { get; set; }
        public Designation Designation { get; set; } = null!;
        public string Status { get; set; } = null!;

        // Every employee belongs to a department
        public int DepartmentId { get; set; }
        public Department Department { get; set; } = null!;

        // Every employee belongs to a branch
        public int BranchId { get; set; }
        public Branch Branch { get; set; } = null!;
    }
}
