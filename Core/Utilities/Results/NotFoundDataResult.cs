namespace Core.Utilities.Results;

public sealed class NotFoundDataResult<T> : ErrorDataResult<T>, INotFoundResult
{
    public NotFoundDataResult(string message) : base(message) { }
}
