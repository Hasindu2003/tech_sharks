using HRMS.Application.Common;
using HRMS.Application.Entity.Commands;

namespace HRMS.Application.Designations.Commands
{
    public class EditDesignationCommand : ICommand<Result>
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public int DepartmentId { get; set; }
    }
}
