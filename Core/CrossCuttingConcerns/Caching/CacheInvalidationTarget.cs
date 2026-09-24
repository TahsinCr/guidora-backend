using System.Collections.Generic;

namespace Core.CrossCuttingConcerns.Caching;

public sealed record CacheInvalidationTarget(string Profile, int? ArgumentIndex = null, object? ArgumentValue = null);

/// <summary>Internal cache work to perform after a successful use case commits.</summary>
public interface ICacheInvalidationDirective
{
    IReadOnlyCollection<CacheInvalidationTarget> CacheInvalidations { get; }
}
