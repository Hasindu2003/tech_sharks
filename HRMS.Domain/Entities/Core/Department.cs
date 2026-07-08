using System.Collections.Generic;

namespace HRMS.Domain.Entities.Core
{
    public class Department
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;

        public ICollection<BranchDepartment> BranchDepartments { get; set; } = new List<BranchDepartment>();
        public ICollection<DepartmentDesignation> DepartmentDesignations { get; set; } = new List<DepartmentDesignation>();
        public ICollection<Employee> Employees { get; set; } = new List<Employee>();
    }
}

