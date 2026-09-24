namespace Core.Utilities.Results;

public sealed class ConflictDataResult<T> : ErrorDataResult<T>, IConflictResult
{
    public ConflictDataResult(string message) : base(message) { }
}
