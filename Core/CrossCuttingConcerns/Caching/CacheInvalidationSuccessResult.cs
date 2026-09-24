using System;
using System.Collections.Generic;
using System.Linq;
using Core.Utilities.Results;

namespace Core.CrossCuttingConcerns.Caching;

/// <summary>A successful result with cache targets consumed by the interception pipeline.</summary>
public sealed class CacheInvalidationSuccessResult : SuccessResult, ICacheInvalidationDirective
{
    private readonly IReadOnlyCollection<CacheInvalidationTarget> _cacheInvalidations;

    public CacheInvalidationSuccessResult(string message, params CacheInvalidationTarget[] cacheInvalidations)
        : base(message)
    {
        ArgumentNullException.ThrowIfNull(cacheInvalidations);
        if (cacheInvalidations.Any(target => target is null || string.IsNullOrWhiteSpace(target.Profile)))
        {
            throw new ArgumentException("Each cache invalidation target must specify a profile.", nameof(cacheInvalidations));
        }

        _cacheInvalidations = Array.AsReadOnly(cacheInvalidations.ToArray());
    }

    IReadOnlyCollection<CacheInvalidationTarget> ICacheInvalidationDirective.CacheInvalidations => _cacheInvalidations;
}
