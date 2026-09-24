namespace Core.Utilities.Results;

public sealed class NotFoundResult : ErrorResult, INotFoundResult
{
    public NotFoundResult(string message) : base(message) { }
}
