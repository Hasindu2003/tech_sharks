namespace HRMS.Domain.Entities.Core
{
    public class DepartmentDesignation
    {
        public int Id { get; set; }
        public int DepartmentId { get; set; }
        public Department Department { get; set; } = null!;
        public int DesignationId { get; set; }
        public Designation Designation { get; set; } = null!;
    }
}
