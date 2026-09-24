using Newtonsoft.Json;
using System;
using System.Security.Cryptography;
using System.Text;

namespace Core.CrossCuttingConcerns.Caching;

public static class CacheKeySegment
{
    public static string Hash(object? value)
    {
        var serialized = JsonConvert.SerializeObject(value);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(serialized)));
    }
}
