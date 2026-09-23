using System;
using System.Linq;
using Newtonsoft.Json;
using StackExchange.Redis;

namespace Core.CrossCuttingConcerns.Caching.Redis
{
    public class RedisCacheManager : ICacheManager
    {
        private const string CachePrefix = "cache:";
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
            var value = GetDatabase().StringGet(ToRedisKey(key));
            if (value.IsNullOrEmpty)
            {
                return null;
            }

            var envelope = JsonConvert.DeserializeObject<CacheValueEnvelope>(value!);
            if (envelope == null || string.IsNullOrWhiteSpace(envelope.TypeName))
            {
                return null;
            }

            var valueType = Type.GetType(envelope.TypeName, throwOnError: false);
            return valueType == null ? null : JsonConvert.DeserializeObject(envelope.Payload, valueType);
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

                var keys = server.Keys(database: 0, pattern: redisPattern, pageSize: 1000).ToArray();
                if (keys.Length > 0)
                {
                    database.KeyDelete(keys);
                }
            }
        }

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
