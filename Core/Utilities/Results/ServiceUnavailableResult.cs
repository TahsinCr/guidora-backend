namespace Core.Utilities.Results;

public sealed class ServiceUnavailableResult(string message) : ErrorResult(message), IServiceUnavailableResult;
