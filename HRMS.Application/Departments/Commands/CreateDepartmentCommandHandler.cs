using HRMS.Application.Common;
using HRMS.Application.Entity.Commands;
using HRMS.Domain.Entities.Core;
using HRMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Application.Departments.Commands
{
    public class CreateDepartmentCommandHandler : ICommandHandler<CreateDepartmentCommand, Result>
    {
        private readonly ApplicationDbContext _context;

        public CreateDepartmentCommandHandler(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<Result> HandleAsync(CreateDepartmentCommand command)
        {
            bool nameExists = await _context.Departments
                .AnyAsync(d => d.Name.ToLower() == command.Name.Trim().ToLower());

            if (nameExists)
                return Result.Failure($"A department named '{command.Name}' already exists.");

            _context.Departments.Add(new Department { Name = command.Name.Trim() });
            await _context.SaveChangesAsync();

            return Result.Success();
        }
    }
}
