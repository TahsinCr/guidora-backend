using Core.Aspects.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Tests.Core.Aspects;

public sealed class LogParameterRedactorTests
{
    [Fact]
    public void RedactsSensitiveFieldsRecursivelyWithoutMutatingTheRequest()
    {
        var request = new AuthenticationRequest
        {
            Email = "user@example.test",
            Password = "plain-password",
            RefreshToken = "refresh-secret",
            Metadata = new ApiCredentials
            {
                ApiKey = "api-key-secret",
                DisplayName = "public-value"
            }
        };

        var sanitized = Assert.IsType<JObject>(LogParameterRedactor.Redact("request", request));

        Assert.Equal("user@example.test", sanitized.Value<string>(nameof(AuthenticationRequest.Email)));
        Assert.Equal("[REDACTED]", sanitized.Value<string>(nameof(AuthenticationRequest.Password)));
        Assert.Equal("[REDACTED]", sanitized.Value<string>("refresh_token"));
        Assert.Equal("[REDACTED]", sanitized[nameof(AuthenticationRequest.Metadata)]?
            .Value<string>(nameof(ApiCredentials.ApiKey)));
        Assert.Equal("public-value", sanitized[nameof(AuthenticationRequest.Metadata)]?
            .Value<string>(nameof(ApiCredentials.DisplayName)));
        Assert.Equal("plain-password", request.Password);
        Assert.Equal("refresh-secret", request.RefreshToken);
        Assert.Equal("api-key-secret", request.Metadata.ApiKey);
    }

    [Theory]
    [InlineData("access_token")]
    [InlineData("Authorization")]
    [InlineData("cookieValue")]
    [InlineData("private_key")]
    [InlineData("credential")]
    public void RedactsSensitiveTopLevelArguments(string argumentName)
    {
        Assert.Equal("[REDACTED]", LogParameterRedactor.Redact(argumentName, "secret-value"));
    }

    [Fact]
    public void FailsClosedWhenAnObjectCannotBeSerialized()
    {
        var value = new UnsupportedLogValue();

        var sanitized = LogParameterRedactor.Redact("request", value);

        Assert.Equal("<UnsupportedLogValue omitted from logs>", sanitized);
    }

    public sealed class AuthenticationRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        [JsonProperty("refresh_token")]
        public string RefreshToken { get; set; } = string.Empty;
        public ApiCredentials Metadata { get; set; } = new();
    }

    public sealed class ApiCredentials
    {
        public string ApiKey { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
    }

    public sealed class UnsupportedLogValue
    {
        public string Unreadable => throw new InvalidOperationException("sensitive diagnostic text");

        public override string ToString() => "sensitive fallback text";
    }
}
