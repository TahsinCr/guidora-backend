namespace Core.Utilities.Results;

public sealed class ForbiddenResult : ErrorResult, IForbiddenResult
{
    public ForbiddenResult(string message) : base(message) { }
}
