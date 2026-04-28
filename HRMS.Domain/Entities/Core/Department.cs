using System;
using System.Collections.Generic;

namespace HRMS.Domain.Entities.Core
{
    public class Department
    {
        public int Id { get; set; }   // Primary Key
        public string Name { get; set; } = null!;  // Could be "General" or "Support" for cleaning staff
        public string? Description { get; set; }
        public int Capacity { get; set; }  // Max number of employees in this department

        // Foreign key to Branch
        public int BranchId { get; set; }
        public Branch Branch { get; set; } = null!;

        // One Department → Many Employees
        public ICollection<Employee> Employees { get; set; } = new List<Employee>();
    }
}
