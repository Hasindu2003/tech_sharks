namespace HRMS.Application.Entity.Commands
{
    /// <summary>
    /// Generic command to update any entity.
    /// Returns true if update succeeded, false otherwise.
    /// </summary>
    public class UpdateEntityCommand<TEntity> : ICommand<bool> where TEntity : class
    {
        public TEntity Entity { get; }

        public UpdateEntityCommand(TEntity entity)
        {
            Entity = entity;
        }
    }
}
