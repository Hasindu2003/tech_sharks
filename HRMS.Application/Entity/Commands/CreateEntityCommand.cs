namespace HRMS.Application.Entity.Commands
{
    /// <summary>
    /// Generic command to create any entity.
    /// Returns the created entity with its generated Id.
    /// </summary>
    public class CreateEntityCommand<TEntity> : ICommand<TEntity> where TEntity : class
    {
        public TEntity Entity { get; }

        public CreateEntityCommand(TEntity entity)
        {
            Entity = entity;
        }
    }
}
