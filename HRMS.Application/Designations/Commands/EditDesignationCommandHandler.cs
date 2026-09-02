using HRMS.Application.Common;
using HRMS.Application.Entity.Commands;
using HRMS.Domain.Entities.Core;
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

            // Enforce strictly one department assignment per designation
            var existingDDs = await _context.DepartmentDesignations
                .Where(dd => dd.DesignationId == command.Id)
                .ToListAsync();
            _context.DepartmentDesignations.RemoveRange(existingDDs);

            if (command.DepartmentId > 0)
            {
                _context.DepartmentDesignations.Add(new DepartmentDesignation
                {
                    DepartmentId = command.DepartmentId,
                    DesignationId = command.Id
                });
            }

            await _context.SaveChangesAsync();

            return Result.Success();
        }
    }
}
