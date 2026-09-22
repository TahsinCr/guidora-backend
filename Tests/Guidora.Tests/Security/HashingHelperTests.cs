using Core.Utilities.Security.Hashing;

namespace Guidora.Tests.Security;

public sealed class HashingHelperTests
{
    [Fact]
    public void CreatePasswordHash_creates_a_hash_that_verifies_the_original_password()
    {
        HashingHelper.CreatePasswordHash("correct horse battery staple", out var hash, out var salt);

        var verified = HashingHelper.VerifyPasswordHash("correct horse battery staple", hash, salt);

        Assert.True(verified);
    }

    [Fact]
    public void VerifyPasswordHash_rejects_an_incorrect_password()
    {
        HashingHelper.CreatePasswordHash("correct horse battery staple", out var hash, out var salt);

        var verified = HashingHelper.VerifyPasswordHash("incorrect password", hash, salt);

        Assert.False(verified);
    }
}
