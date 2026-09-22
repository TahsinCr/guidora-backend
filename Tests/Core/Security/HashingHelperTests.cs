using Core.Utilities.Security.Hashing;

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
}
