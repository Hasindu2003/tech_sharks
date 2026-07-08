using HRMS.Application.Common;
using HRMS.Application.Entity.Commands;

namespace HRMS.Application.Branches.Commands
{
    public class CreateBranchCommand : ICommand<Result>
    {
        public string Name { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
    }
}
