using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Authorization;
using WebAPI.Controllers;

namespace Tests.WebAPI.Controllers;

public sealed class AuthControllerContractTests
{
    [Theory]
    [InlineData(nameof(AuthController.Login), "auth-login")]
    [InlineData(nameof(AuthController.Register), "auth-register")]
    [InlineData(nameof(AuthController.Refresh), "auth-refresh")]
    [InlineData(nameof(AuthController.RequestEmailVerification), "email-verification-request")]
    [InlineData(nameof(AuthController.ConfirmEmailVerification), "email-verification-confirm")]
    public void AuthenticationEndpointsUseTheirConfiguredRateLimitPolicy(string methodName, string expectedPolicy)
    {
        var method = typeof(AuthController).GetMethod(methodName);

        Assert.NotNull(method);
        var rateLimit = Assert.Single(method!.GetCustomAttributes(typeof(EnableRateLimitingAttribute), inherit: true));
        Assert.Equal(expectedPolicy, Assert.IsType<EnableRateLimitingAttribute>(rateLimit).PolicyName);
    }

    [Theory]
    [InlineData(nameof(AuthController.RequestEmailVerification))]
    [InlineData(nameof(AuthController.ConfirmEmailVerification))]
    public void EmailVerificationEndpointsRequireAuthentication(string methodName)
    {
        var method = typeof(AuthController).GetMethod(methodName)!;
        Assert.Single(method.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true));
    }
}
