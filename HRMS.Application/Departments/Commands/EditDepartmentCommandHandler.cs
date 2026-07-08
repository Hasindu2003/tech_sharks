using HRMS.Application.Common;
using HRMS.Application.Entity.Commands;
using HRMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Application.Departments.Commands
{
    public class EditDepartmentCommandHandler : ICommandHandler<EditDepartmentCommand, Result>
    {
        private readonly ApplicationDbContext _context;

        public EditDepartmentCommandHandler(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<Result> HandleAsync(EditDepartmentCommand command)
        {
            bool nameExists = await _context.Departments
                .AnyAsync(d => d.Id != command.Id && d.Name.ToLower() == command.Name.Trim().ToLower());

            if (nameExists)
                return Result.Failure($"A department named '{command.Name}' already exists.");

            var department = await _context.Departments.FindAsync(command.Id);
            if (department == null)
                return Result.Failure("Department not found.");

            department.Name = command.Name.Trim();

            await _context.SaveChangesAsync();

            return Result.Success();
        }
    }
}
