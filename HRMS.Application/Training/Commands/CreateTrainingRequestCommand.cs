using HRMS.Domain.Entities.Training;
using HRMS.Infrastructure.Persistence;

namespace HRMS.Application.Training.Commands;

public class TrainingService
{
    private readonly ApplicationDbContext _context;

    public TrainingService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<int> CreateRequestAsync(TrainingProgramRequest request)
    {
        _context.TrainingProgramRequests.Add(request);
        await _context.SaveChangesAsync();
        return request.Id;
    }
}