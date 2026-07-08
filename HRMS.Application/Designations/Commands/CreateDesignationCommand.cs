using HRMS.Application.Common;
using HRMS.Application.Entity.Commands;

namespace HRMS.Application.Designations.Commands
{
    public class CreateDesignationCommand : ICommand<Result>
    {
        public string Title { get; set; } = string.Empty;
    }
}
