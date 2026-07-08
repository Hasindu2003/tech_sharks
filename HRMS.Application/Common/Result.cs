namespace HRMS.Application.Common
{
    public class Result
    {
        public bool Succeeded { get; }
        public string? Error { get; }

        private Result(bool succeeded, string? error)
        {
            Succeeded = succeeded;
            Error = error;
        }

        public static Result Success() => new(true, null);
        public static Result Failure(string error) => new(false, error);
    }
}
