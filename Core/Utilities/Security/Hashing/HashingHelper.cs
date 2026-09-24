using System;
using System.Security.Cryptography;
using System.Text;

namespace Core.Utilities.Security.Hashing
{
    public class HashingHelper
    {
        private const int SaltSize = 16;
        private const int HashSize = 32;
        private const int Iterations = 600_000;

        public static void CreatePasswordHash(string password, out byte[] passwordHash, out byte[] passwordSalt)
        {
            passwordSalt = RandomNumberGenerator.GetBytes(SaltSize);
            passwordHash = Rfc2898DeriveBytes.Pbkdf2(password, passwordSalt, Iterations,
                HashAlgorithmName.SHA512, HashSize);
        }

        public static bool VerifyPasswordHash(string password, byte[] passwordHash, byte[] passwordSalt)
        {
            if (passwordSalt.Length == SaltSize && passwordHash.Length == HashSize)
            {
                var computedHash = Rfc2898DeriveBytes.Pbkdf2(password, passwordSalt, Iterations,
                    HashAlgorithmName.SHA512, HashSize);
                return CryptographicOperations.FixedTimeEquals(computedHash, passwordHash);
            }

            // Existing accounts created before the PBKDF2 upgrade retain a
            // temporary login path. Rehash the password on the next successful
            // credential update and remove this branch after the migration window.
            using var legacyHmac = new HMACSHA512(passwordSalt);
            var legacyHash = legacyHmac.ComputeHash(Encoding.UTF8.GetBytes(password));
            return CryptographicOperations.FixedTimeEquals(legacyHash, passwordHash);
        }

        public static bool NeedsRehash(byte[] passwordHash, byte[] passwordSalt) =>
            passwordHash.Length != HashSize || passwordSalt.Length != SaltSize;
    }
}
