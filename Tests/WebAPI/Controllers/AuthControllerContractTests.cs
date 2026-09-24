using Microsoft.AspNetCore.RateLimiting;
using WebAPI.Controllers;

namespace Tests.WebAPI.Controllers;

public sealed class AuthControllerContractTests
{
    [Theory]
    [InlineData(nameof(AuthController.Login), "auth-login")]
    [InlineData(nameof(AuthController.Register), "auth-register")]
    [InlineData(nameof(AuthController.Refresh), "auth-refresh")]
    public void AuthenticationEndpointsUseTheirConfiguredRateLimitPolicy(string methodName, string expectedPolicy)
    {
        var method = typeof(AuthController).GetMethod(methodName);

        Assert.NotNull(method);
        var rateLimit = Assert.Single(method!.GetCustomAttributes(typeof(EnableRateLimitingAttribute), inherit: true));
        Assert.Equal(expectedPolicy, Assert.IsType<EnableRateLimitingAttribute>(rateLimit).PolicyName);
    }
}
