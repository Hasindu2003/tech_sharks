namespace HRMS.Application.Entity.Queries
{
    /// <summary>
    /// Generic handler that executes a query and returns a result.
    /// </summary>
    public interface IQueryHandler<TQuery, TResult> where TQuery : IQuery<TResult>
    {
        Task<TResult> HandleAsync(TQuery query);
    }
}
