using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Core.Utilities.IoC;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace Core.CrossCuttingConcerns.Caching.Microsoft
{
    internal class MemoryCacheManager : ICacheManager
    {
        private readonly IMemoryCache _memoryCache;

        public MemoryCacheManager()
        {
            _memoryCache = ServiceTool.ServiceProvider.GetRequiredService<IMemoryCache>();
        }

        public T Get<T>(string key)
        {
            return _memoryCache.Get<T>(key)!;
        }
        public object? Get(string key)
        {
            return TryGet(key, out var value) ? value : null;
        }

        public bool TryGet(string key, out object? value)
        {
            return _memoryCache.TryGetValue(key, out value);
        }

        public void Add(string key, object? value, int? durationMinutes)
        {
            if (durationMinutes is > 0)
            {
                _memoryCache.Set(key, value, TimeSpan.FromMinutes(durationMinutes.Value));
                return;
            }

            _memoryCache.Set(key, value);
        }

        public bool IsExists(string key)
        {
            return _memoryCache.TryGetValue(key, out _);
        }

        public void Remove(string key)
        {
            _memoryCache.Remove(key);
        }
        public void RemoveByPattern(string pattern)
        {
            var cacheEntriesCollectionDefinition = typeof(MemoryCache).GetProperty(
                "EntriesCollection",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (cacheEntriesCollectionDefinition?.GetValue(_memoryCache) is not System.Collections.IEnumerable cacheEntriesCollection)
            {
                return;
            }

            List<ICacheEntry> cacheCollectionValues = new List<ICacheEntry>();

            foreach (var cacheItem in cacheEntriesCollection)
            {
                var cacheItemValue = cacheItem?.GetType().GetProperty("Value")?.GetValue(cacheItem) as ICacheEntry;
                if (cacheItemValue != null)
                {
                    cacheCollectionValues.Add(cacheItemValue);
                }
            }

            var regexPattern = "^" + Regex.Escape(pattern).Replace("\\*", ".*", StringComparison.Ordinal) + "$";
            var regex = new Regex(regexPattern, RegexOptions.Singleline | RegexOptions.Compiled | RegexOptions.IgnoreCase);
            var keysToRemove = cacheCollectionValues
                .Where(d => regex.IsMatch(d.Key?.ToString() ?? string.Empty))
                .Select(d => d.Key)
                .ToList();

            foreach (var key in keysToRemove)
            {
                _memoryCache.Remove(key);
            }
        }
    }
}
