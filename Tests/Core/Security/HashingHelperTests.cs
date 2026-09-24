using Core.Utilities.Security.Hashing;
using System.Security.Cryptography;
using System.Text;

namespace Tests.Core.Security;

public sealed class HashingHelperTests
{
    [Fact]
    public void CreatePasswordHash_creates_a_hash_that_verifies_the_original_password()
    {
        HashingHelper.CreatePasswordHash("correct horse battery staple", out var hash, out var salt);

        Assert.True(HashingHelper.VerifyPasswordHash("correct horse battery staple", hash, salt));
    }

    [Fact]
    public void VerifyPasswordHash_rejects_an_incorrect_password()
    {
        HashingHelper.CreatePasswordHash("correct horse battery staple", out var hash, out var salt);

        Assert.False(HashingHelper.VerifyPasswordHash("incorrect password", hash, salt));
    }

    [Fact]
    public void Legacy_hash_verifies_and_is_marked_for_rehash()
    {
        const string password = "legacy password";
        var salt = RandomNumberGenerator.GetBytes(64);
        using var hmac = new HMACSHA512(salt);
        var legacyHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(password));

        Assert.True(HashingHelper.VerifyPasswordHash(password, legacyHash, salt));
        Assert.True(HashingHelper.NeedsRehash(legacyHash, salt));

        HashingHelper.CreatePasswordHash(password, out var currentHash, out var currentSalt);
        Assert.False(HashingHelper.NeedsRehash(currentHash, currentSalt));
    }
}
