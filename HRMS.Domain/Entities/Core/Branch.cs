using System;
using System.Collections.Generic;

namespace HRMS.Domain.Entities.Core
{
    public class Branch
    {
        public int Id { get; set; }   // Primary Key
        public string Name { get; set; } = null!;
        public string Location { get; set; } = null!;
        public int Capacity { get; set; }

        // One Branch → Many Departments
        public ICollection<Department> Departments { get; set; } = new List<Department>();

        // One Branch → Many Employees
        public ICollection<Employee> Employees { get; set; } = new List<Employee>();
    }
}
