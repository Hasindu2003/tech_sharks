using System.Collections.Generic;

namespace HRMS.Domain.Entities.Core
{
    public class Designation
    {
        public int Id { get; set; }
        public string Title { get; set; } = null!;

        public ICollection<DepartmentDesignation> DepartmentDesignations { get; set; } = new List<DepartmentDesignation>();
        public ICollection<Employee> Employees { get; set; } = new List<Employee>();
    }
}

