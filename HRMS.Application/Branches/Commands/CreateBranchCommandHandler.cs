using HRMS.Application.Common;
using HRMS.Application.Entity.Commands;
using HRMS.Domain.Entities.Core;
using HRMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Application.Branches.Commands
{
    public class CreateBranchCommandHandler : ICommandHandler<CreateBranchCommand, Result>
    {
        private readonly ApplicationDbContext _context;

        public CreateBranchCommandHandler(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<Result> HandleAsync(CreateBranchCommand command)
        {
            bool nameExists = await _context.Branches
                .AnyAsync(b => b.Name.ToLower() == command.Name.Trim().ToLower());

            if (nameExists)
                return Result.Failure($"A branch named '{command.Name}' already exists.");

            _context.Branches.Add(new Branch
            {
                Name = command.Name.Trim(),
                Location = command.Location.Trim()
            });

            await _context.SaveChangesAsync();

            return Result.Success();
        }
    }
}
