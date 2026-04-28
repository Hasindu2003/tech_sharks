namespace HRMS.Application.Entity.Queries
{
    /// <summary>
    /// Generic query to retrieve all entities of a given type.
    /// Returns a list of all records.
    /// </summary>
    public class GetAllEntitiesQuery<TEntity> : IQuery<List<TEntity>> where TEntity : class
    {
    }
}
