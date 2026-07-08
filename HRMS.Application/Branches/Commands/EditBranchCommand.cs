using HRMS.Application.Common;
using HRMS.Application.Entity.Commands;

namespace HRMS.Application.Branches.Commands
{
    public class EditBranchCommand : ICommand<Result>
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
    }
}
