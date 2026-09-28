using WebAPI.Configuration;

namespace Tests.WebAPI.Configuration;

public sealed class SecretConfigurationPolicyTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("replace-with-a-random-signing-key-of-at-least-32-bytes")]
    [InlineData("replace-with-a-different-random-key-of-at-least-32-bytes")]
    [InlineData("change-me-even-if-padded-to-more-than-thirty-two-bytes")]
    public void RejectsMissingOrTemplateSecrets(string? value) =>
        Assert.False(SecretConfigurationPolicy.IsConfigured(value));

    [Fact]
    public void AcceptsIndependentLongSecret() =>
        Assert.True(SecretConfigurationPolicy.IsConfigured("CiOnlyIndependentRandomLookingKey1234567890"));
}
