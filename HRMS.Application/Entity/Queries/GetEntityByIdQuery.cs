namespace HRMS.Application.Entity.Queries
{
    /// <summary>
    /// Generic query to retrieve a single entity by its Id.
    /// Returns the entity or null if not found.
    /// </summary>
    public class GetEntityByIdQuery<TEntity> : IQuery<TEntity?> where TEntity : class
    {
        public int Id { get; }

        public GetEntityByIdQuery(int id)
        {
            Id = id;
        }
    }
}
