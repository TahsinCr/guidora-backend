using System;
using System.Security.Cryptography;

namespace Core.Utilities.Security.Hashing
{
    public static class RefreshTokenHasher
    {
        public static string CreateToken()
        {
            var bytes = RandomNumberGenerator.GetBytes(64);
            return Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").TrimEnd('=');
        }

        public static string Hash(string token)
        {
            var hash = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token));
            return Convert.ToHexString(hash);
        }
    }
}
