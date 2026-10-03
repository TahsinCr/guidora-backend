using Core.CrossCuttingConcerns.Caching;

namespace Tests.Core.TestDoubles;

public sealed class CacheManagerStub : ICacheManager
{
    public CacheManagerStub(List<string>? events = null) => Events = events ?? new List<string>();
    public List<string> InvalidatedPatterns { get; } = new();
    public List<string> Events { get; }
    public Dictionary<string, object?> Values { get; } = new();
    public Dictionary<string, int?> RequestedDurations { get; } = new();

    public Task? AsyncReadGate { get; set; }
    public async Task<long> GetProfileGenerationAsync(string profile)
    {
        if (AsyncReadGate is not null) await AsyncReadGate;
        return 0;
    }

    public T Get<T>(string key) => (T)Get(key)!;
    public object? Get(string key) => Values.TryGetValue(key, out var value) ? value : null;
    public bool TryGet(string key, out object? value) => Values.TryGetValue(key, out value);
    public void Add(string key, object? value, int? durationMinutes)
    {
        Values[key] = value;
        RequestedDurations[key] = durationMinutes;
    }
    public bool IsExists(string key) => Values.ContainsKey(key);
    public void Remove(string key) => Values.Remove(key);
    public void RemoveByPattern(string pattern)
    {
        InvalidatedPatterns.Add(pattern);
        Events.Add($"invalidate:{pattern}");
    }
}
