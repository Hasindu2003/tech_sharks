using System.Collections.Generic;

namespace HRMS.Domain.Entities.Core
{
    public class Designation
    {
        public int Id { get; set; }  // PK
        public string Title { get; set; } = null!;  // e.g., "Software Engineer", "Manager"
        public string? Description { get; set; }   // Optional notes

        // Employees with this designation
        public ICollection<Employee> Employees { get; set; } = new List<Employee>();
    }
}

