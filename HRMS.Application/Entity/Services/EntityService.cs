using HRMS.Application.Entity.Requests;
using HRMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Application.Entity.Services
{
    /// <summary>
    /// Generic CRUD service implementation.
    /// Uses ApplicationDbContext to perform operations on any entity type.
    /// 
    /// One class handles ALL entities — no need to write separate services
    /// for Branch, Employee, Leave, etc.
    /// </summary>
    public class EntityService<TEntity> : IEntityService<TEntity> where TEntity : class
    {
        private readonly ApplicationDbContext _context;
        private readonly DbSet<TEntity> _dbSet;

        public EntityService(ApplicationDbContext context)
        {
            _context = context;
            _dbSet = context.Set<TEntity>();
        }

        public async Task<EntityResponse<TEntity>> GetByIdAsync(int id)
        {
            var entity = await _dbSet.FindAsync(id);

            if (entity is null)
                return EntityResponse<TEntity>.FailureResponse($"{typeof(TEntity).Name} with Id {id} not found.");

            return EntityResponse<TEntity>.SuccessResponse(entity);
        }

        public async Task<EntityResponse<List<TEntity>>> GetAllAsync()
        {
            var entities = await _dbSet.ToListAsync();
            return EntityResponse<List<TEntity>>.SuccessResponse(entities);
        }

        public async Task<EntityResponse<TEntity>> CreateAsync(EntityRequest<TEntity> request)
        {
            await _dbSet.AddAsync(request.Entity);
            await _context.SaveChangesAsync();

            return EntityResponse<TEntity>.SuccessResponse(request.Entity, $"{typeof(TEntity).Name} created successfully.");
        }

        public async Task<EntityResponse<TEntity>> UpdateAsync(EntityRequest<TEntity> request)
        {
            _dbSet.Update(request.Entity);
            await _context.SaveChangesAsync();

            return EntityResponse<TEntity>.SuccessResponse(request.Entity, $"{typeof(TEntity).Name} updated successfully.");
        }

        public async Task<EntityResponse<TEntity>> DeleteAsync(int id)
        {
            var entity = await _dbSet.FindAsync(id);

            if (entity is null)
                return EntityResponse<TEntity>.FailureResponse($"{typeof(TEntity).Name} with Id {id} not found.");

            _dbSet.Remove(entity);
            await _context.SaveChangesAsync();

            return EntityResponse<TEntity>.SuccessResponse(entity, $"{typeof(TEntity).Name} deleted successfully.");
        }
    }
}
