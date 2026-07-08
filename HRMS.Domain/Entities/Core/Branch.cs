using System;
using System.Collections.Generic;

namespace HRMS.Domain.Entities.Core
{
    public class Branch
    {
        public int Id { get; set; }   // Primary Key
        public string Name { get; set; } = null!;
        public string Location { get; set; } = null!;

        public ICollection<BranchDepartment> BranchDepartments { get; set; } = new List<BranchDepartment>();

        // One Branch → Many Employees
        public ICollection<Employee> Employees { get; set; } = new List<Employee>();
    }
}
