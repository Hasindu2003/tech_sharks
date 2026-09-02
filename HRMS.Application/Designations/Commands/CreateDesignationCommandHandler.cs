using HRMS.Application.Common;
using HRMS.Application.Entity.Commands;
using HRMS.Domain.Entities.Core;
using HRMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Application.Designations.Commands
{
    public class CreateDesignationCommandHandler : ICommandHandler<CreateDesignationCommand, Result>
    {
        private readonly ApplicationDbContext _context;

        public CreateDesignationCommandHandler(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<Result> HandleAsync(CreateDesignationCommand command)
        {
            bool titleExists = await _context.Designations
                .AnyAsync(d => d.Title.ToLower() == command.Title.Trim().ToLower());

            if (titleExists)
                return Result.Failure($"A designation titled '{command.Title}' already exists.");

            var designation = new Designation { Title = command.Title.Trim() };
            _context.Designations.Add(designation);
            await _context.SaveChangesAsync();

            if (command.DepartmentId > 0)
            {
                _context.DepartmentDesignations.Add(new DepartmentDesignation
                {
                    DepartmentId = command.DepartmentId,
                    DesignationId = designation.Id
                });
                await _context.SaveChangesAsync();
            }

            return Result.Success();
        }
    }
}
