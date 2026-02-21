namespace HRMS.Application.Entity.Requests
{
    /// <summary>
    /// Generic request wrapper for create/update operations.
    /// Carries the entity data from the UI layer to the Application layer.
    /// </summary>
    public class EntityRequest<TEntity> where TEntity : class
    {
        public TEntity Entity { get; set; } = null!;
    }

    /// <summary>
    /// Generic response wrapper returned from the Application layer.
    /// Carries the result data + success/error info back to the UI layer.
    /// </summary>
    public class EntityResponse<TEntity> where TEntity : class
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public TEntity? Data { get; set; }
        public List<string> Errors { get; set; } = new();

        public static EntityResponse<TEntity> SuccessResponse(TEntity data, string? message = null)
        {
            return new EntityResponse<TEntity>
            {
                Success = true,
                Data = data,
                Message = message
            };
        }

        public static EntityResponse<TEntity> FailureResponse(string error)
        {
            return new EntityResponse<TEntity>
            {
                Success = false,
                Errors = new List<string> { error }
            };
        }

        public static EntityResponse<TEntity> FailureResponse(List<string> errors)
        {
            return new EntityResponse<TEntity>
            {
                Success = false,
                Errors = errors
            };
        }
    }
}
