namespace HRMS.Application.Entity.Commands
{
    /// <summary>
    /// Generic handler that executes a command and returns a result.
    /// </summary>
    public interface ICommandHandler<TCommand, TResult> where TCommand : ICommand<TResult>
    {
        Task<TResult> HandleAsync(TCommand command);
    }
}
