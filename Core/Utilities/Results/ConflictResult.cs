namespace Core.Utilities.Results;

public sealed class ConflictResult : ErrorResult, IConflictResult
{
    public ConflictResult(string message) : base(message) { }
}
