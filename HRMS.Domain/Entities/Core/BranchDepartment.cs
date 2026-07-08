namespace HRMS.Domain.Entities.Core
{
    public class BranchDepartment
    {
        public int Id { get; set; }
        public int BranchId { get; set; }
        public Branch Branch { get; set; } = null!;
        public int DepartmentId { get; set; }
        public Department Department { get; set; } = null!;
    }
}
