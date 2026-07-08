using HRMS.Application.Common;
using HRMS.Application.Entity.Commands;

namespace HRMS.Application.Departments.Commands
{
    public class CreateDepartmentCommand : ICommand<Result>
    {
        public string Name { get; set; } = string.Empty;
    }
}
