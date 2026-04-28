namespace HRMS.Application.Entity.Commands
{
    /// <summary>
    /// Generic command to delete any entity by its Id.
    /// Returns true if deletion succeeded, false otherwise.
    /// </summary>
    public class DeleteEntityCommand<TEntity> : ICommand<bool> where TEntity : class
    {
        public int Id { get; }

        public DeleteEntityCommand(int id)
        {
            Id = id;
        }
    }
}
