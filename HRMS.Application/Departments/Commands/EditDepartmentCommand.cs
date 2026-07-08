using HRMS.Application.Common;
using HRMS.Application.Entity.Commands;

namespace HRMS.Application.Departments.Commands
{
    public class EditDepartmentCommand : ICommand<Result>
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}
