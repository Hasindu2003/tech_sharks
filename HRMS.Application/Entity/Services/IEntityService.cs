using HRMS.Application.Entity.Requests;

namespace HRMS.Application.Entity.Services
{
    /// <summary>
    /// Generic CRUD service interface.
    /// Any entity type can be plugged in using generics.
    /// 
    /// Example usage:
    ///   IEntityService<Branch>   → handles Branch CRUD
    ///   IEntityService<Employee> → handles Employee CRUD
    ///   IEntityService<Leave>    → handles Leave CRUD
    /// </summary>
    public interface IEntityService<TEntity> where TEntity : class
    {
        /// <summary>Get a single entity by its Id.</summary>
        Task<EntityResponse<TEntity>> GetByIdAsync(int id);

        /// <summary>Get all entities of this type.</summary>
        Task<EntityResponse<List<TEntity>>> GetAllAsync();

        /// <summary>Create a new entity.</summary>
        Task<EntityResponse<TEntity>> CreateAsync(EntityRequest<TEntity> request);

        /// <summary>Update an existing entity.</summary>
        Task<EntityResponse<TEntity>> UpdateAsync(EntityRequest<TEntity> request);

        /// <summary>Delete an entity by its Id.</summary>
        Task<EntityResponse<TEntity>> DeleteAsync(int id);
    }
}
