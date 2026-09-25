using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.CrossCuttingConcerns.Caching
{
    public interface ICacheManager
    {
        T Get<T>(string key);
        object? Get(string key);
        bool TryGet(string key, out object? value);

        void Add(string key, object? value, int? durationMinutes);

        bool IsExists(string key);

        void Remove(string key);
        void RemoveByPattern(string pattern);
        long GetProfileGeneration(string profile) => 0;
        void AdvanceProfileGeneration(string profile) => RemoveByPattern($"{profile}:*");

        Task<(bool Found, object? Value)> TryGetAsync(string key) =>
            Task.FromResult(TryGet(key, out var value) ? (true, value) : (false, (object?)null));
        Task AddAsync(string key, object? value, int? durationMinutes)
        {
            Add(key, value, durationMinutes);
            return Task.CompletedTask;
        }
        Task RemoveByPatternAsync(string pattern)
        {
            RemoveByPattern(pattern);
            return Task.CompletedTask;
        }
        Task<long> GetProfileGenerationAsync(string profile) => Task.FromResult(GetProfileGeneration(profile));
        Task AdvanceProfileGenerationAsync(string profile)
        {
            AdvanceProfileGeneration(profile);
            return Task.CompletedTask;
        }
    }
}
