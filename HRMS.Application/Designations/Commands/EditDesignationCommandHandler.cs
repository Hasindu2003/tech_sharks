using HRMS.Application.Common;
using HRMS.Application.Entity.Commands;
using HRMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Application.Designations.Commands
{
    public class EditDesignationCommandHandler : ICommandHandler<EditDesignationCommand, Result>
    {
        private readonly ApplicationDbContext _context;

        public EditDesignationCommandHandler(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<Result> HandleAsync(EditDesignationCommand command)
        {
            bool titleExists = await _context.Designations
                .AnyAsync(d => d.Id != command.Id && d.Title.ToLower() == command.Title.Trim().ToLower());

            if (titleExists)
                return Result.Failure($"A designation titled '{command.Title}' already exists.");

            var designation = await _context.Designations.FindAsync(command.Id);
            if (designation == null)
                return Result.Failure("Designation not found.");

            designation.Title = command.Title.Trim();

            await _context.SaveChangesAsync();

            return Result.Success();
        }
    }
}
