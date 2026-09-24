namespace Core.Utilities.Results;

public sealed class ConflictResult : ErrorResult
{
    public ConflictResult(string message) : base(message) { }
}
