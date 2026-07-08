using HRMS.Application.Common;
using HRMS.Application.Entity.Commands;
using HRMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Application.Branches.Commands
{
    public class EditBranchCommandHandler : ICommandHandler<EditBranchCommand, Result>
    {
        private readonly ApplicationDbContext _context;

        public EditBranchCommandHandler(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<Result> HandleAsync(EditBranchCommand command)
        {
            bool nameExists = await _context.Branches
                .AnyAsync(b => b.Id != command.Id && b.Name.ToLower() == command.Name.Trim().ToLower());

            if (nameExists)
                return Result.Failure($"A branch named '{command.Name}' already exists.");

            var branch = await _context.Branches.FindAsync(command.Id);
            if (branch == null)
                return Result.Failure("Branch not found.");

            branch.Name = command.Name.Trim();
            branch.Location = command.Location.Trim();

            await _context.SaveChangesAsync();

            return Result.Success();
        }
    }
}
