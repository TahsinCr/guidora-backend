using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Core.CrossCuttingConcerns.Caching;
using StackExchange.Redis;

namespace Core.CrossCuttingConcerns.Caching.Redis
{
    public class RedisCacheManager : ICacheManager
    {
        private const string CachePrefix = "cache:";
        private const int KeyDeleteBatchSize = 256;
        private readonly IConnectionMultiplexer _connectionMultiplexer;

        public RedisCacheManager(IConnectionMultiplexer connectionMultiplexer)
        {
            _connectionMultiplexer = connectionMultiplexer;
        }

        public T Get<T>(string key)
        {
            return (T)Get(key)!;
        }

        public object? Get(string key)
        {
            return TryGet(key, out var value) ? value : null;
        }

        public bool TryGet(string key, out object? value)
        {
            value = null;
            var redisKey = ToRedisKey(key);
            var serializedEnvelope = GetDatabase().StringGet(redisKey);
            if (serializedEnvelope.IsNullOrEmpty)
            {
                return false;
            }

            try
            {
                var envelope = JsonConvert.DeserializeObject<CacheValueEnvelope>(serializedEnvelope!);
                if (envelope == null || string.IsNullOrWhiteSpace(envelope.TypeName))
                {
                    GetDatabase().KeyDelete(redisKey);
                    return false;
                }

                var valueType = Type.GetType(envelope.TypeName, throwOnError: false);
                value = valueType == null ? null : JsonConvert.DeserializeObject(envelope.Payload, valueType);
                if (value != null)
                {
                    return true;
                }

                GetDatabase().KeyDelete(redisKey);
                return false;
            }
            catch (JsonException)
            {
                GetDatabase().KeyDelete(redisKey);
                return false;
            }
        }

        public async Task<(bool Found, object? Value)> TryGetAsync(string key)
        {
            var redisKey = ToRedisKey(key);
            var serializedEnvelope = await GetDatabase().StringGetAsync(redisKey).ConfigureAwait(false);
            if (serializedEnvelope.IsNullOrEmpty) return (false, null);

            try
            {
                var envelope = JsonConvert.DeserializeObject<CacheValueEnvelope>(serializedEnvelope!);
                if (envelope is not null && !string.IsNullOrWhiteSpace(envelope.TypeName))
                {
                    var valueType = Type.GetType(envelope.TypeName, throwOnError: false);
                    var value = valueType is null ? null : JsonConvert.DeserializeObject(envelope.Payload, valueType);
                    if (value is not null) return (true, value);
                }
            }
            catch (JsonException)
            {
                // A malformed cache entry is a miss and is removed below.
            }

            await GetDatabase().KeyDeleteAsync(redisKey).ConfigureAwait(false);
            return (false, null);
        }

        public void Add(string key, object? value, int? durationMinutes)
        {
            if (value == null)
            {
                return;
            }

            var valueType = value.GetType();
            var envelope = new CacheValueEnvelope
            {
                TypeName = valueType.AssemblyQualifiedName ?? valueType.FullName ?? valueType.Name,
                Payload = JsonConvert.SerializeObject(value)
            };

            TimeSpan? expiration = durationMinutes is > 0
                ? TimeSpan.FromMinutes(durationMinutes.Value)
                : null;
            GetDatabase().StringSet(ToRedisKey(key), JsonConvert.SerializeObject(envelope), expiration,
                keepTtl: false, when: When.Always);
        }

        public async Task AddAsync(string key, object? value, int? durationMinutes)
        {
            if (value is null) return;
            var valueType = value.GetType();
            var envelope = new CacheValueEnvelope
            {
                TypeName = valueType.AssemblyQualifiedName ?? valueType.FullName ?? valueType.Name,
                Payload = JsonConvert.SerializeObject(value)
            };
            TimeSpan? expiration = durationMinutes is > 0
                ? TimeSpan.FromMinutes(durationMinutes.Value)
                : null;
            await GetDatabase().StringSetAsync(ToRedisKey(key), JsonConvert.SerializeObject(envelope),
                expiration, keepTtl: false, when: When.Always).ConfigureAwait(false);
        }

        public bool IsExists(string key)
        {
            return GetDatabase().KeyExists(ToRedisKey(key));
        }

        public void Remove(string key)
        {
            GetDatabase().KeyDelete(ToRedisKey(key));
        }

        public void RemoveByPattern(string pattern)
        {
            var database = GetDatabase();
            var redisPattern = ToRedisKey(pattern);
            var endpoints = _connectionMultiplexer.GetEndPoints();
            foreach (var endpoint in endpoints)
            {
                var server = _connectionMultiplexer.GetServer(endpoint);
                if (!server.IsConnected)
                {
                    continue;
                }

                var batch = new List<RedisKey>(KeyDeleteBatchSize);
                foreach (var key in server.Keys(database: 0, pattern: redisPattern, pageSize: KeyDeleteBatchSize))
                {
                    batch.Add(key);
                    if (batch.Count < KeyDeleteBatchSize)
                    {
                        continue;
                    }

                    database.KeyDelete(batch.ToArray());
                    batch.Clear();
                }

                if (batch.Count > 0)
                {
                    database.KeyDelete(batch.ToArray());
                }
            }
        }

        public async Task RemoveByPatternAsync(string pattern)
        {
            var database = GetDatabase();
            var redisPattern = ToRedisKey(pattern);
            foreach (var endpoint in _connectionMultiplexer.GetEndPoints())
            {
                var server = _connectionMultiplexer.GetServer(endpoint);
                if (!server.IsConnected) continue;

                var batch = new List<RedisKey>(KeyDeleteBatchSize);
                await foreach (var key in server.KeysAsync(database: 0, pattern: redisPattern,
                                   pageSize: KeyDeleteBatchSize).ConfigureAwait(false))
                {
                    batch.Add(key);
                    if (batch.Count < KeyDeleteBatchSize) continue;
                    await database.KeyDeleteAsync(batch.ToArray()).ConfigureAwait(false);
                    batch.Clear();
                }

                if (batch.Count > 0)
                    await database.KeyDeleteAsync(batch.ToArray()).ConfigureAwait(false);
            }
        }

        public long GetProfileGeneration(string profile) =>
            (long?)GetDatabase().StringGet(ToRedisKey($"cache-generation:{profile}")) ?? 0L;

        public void AdvanceProfileGeneration(string profile) =>
            GetDatabase().StringIncrement(ToRedisKey($"cache-generation:{profile}"));

        public async Task<long> GetProfileGenerationAsync(string profile) =>
            (long?)await GetDatabase().StringGetAsync(ToRedisKey($"cache-generation:{profile}"))
                .ConfigureAwait(false) ?? 0L;

        public async Task AdvanceProfileGenerationAsync(string profile) =>
            await GetDatabase().StringIncrementAsync(ToRedisKey($"cache-generation:{profile}"))
                .ConfigureAwait(false);

        private IDatabase GetDatabase()
        {
            return _connectionMultiplexer.GetDatabase();
        }

        private static string ToRedisKey(string key)
        {
            return $"{CachePrefix}{key}";
        }

        private class CacheValueEnvelope
        {
            public string TypeName { get; set; } = string.Empty;
            public string Payload { get; set; } = string.Empty;
        }
    }
}
