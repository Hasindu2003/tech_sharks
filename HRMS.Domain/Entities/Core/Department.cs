using System;
using System.Collections.Generic;

namespace HRMS.Domain.Entities.Core
{
    public class Department
    {
        public int Id { get; set; }   // Primary Key
        public string Name { get; set; } = null!;
        public string? Description { get; set; }

        // Foreign key to Branch
        public int BranchId { get; set; }
        public Branch Branch { get; set; } = null!;

        // One Department → Many Employees
        public ICollection<Employee> Employees { get; set; } = new List<Employee>();
    }
}
