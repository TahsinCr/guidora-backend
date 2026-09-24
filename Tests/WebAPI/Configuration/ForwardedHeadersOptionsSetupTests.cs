using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WebAPI.Configuration;

namespace Tests.WebAPI.Configuration;

public sealed class ForwardedHeadersOptionsSetupTests
{
    [Fact]
    public void ConfiguresForwardedHeadersAndAddsOnlyTheConfiguredProxy()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ForwardedHeaders:KnownProxies:0"] = "10.20.30.40"
            })
            .Build();
        var options = new ForwardedHeadersOptions();

        new ForwardedHeadersOptionsSetup(configuration).Configure(options);

        Assert.Equal(ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto, options.ForwardedHeaders);
        Assert.Contains(IPAddress.Parse("10.20.30.40"), options.KnownProxies);
        Assert.DoesNotContain(IPAddress.Parse("10.20.30.41"), options.KnownProxies);
    }

    [Fact]
    public void IgnoresAnEmptyOptionalProxySetting()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ForwardedHeaders:KnownProxies:0"] = string.Empty
            })
            .Build();
        var defaultProxies = new ForwardedHeadersOptions().KnownProxies.Order().ToArray();
        var options = new ForwardedHeadersOptions();

        new ForwardedHeadersOptionsSetup(configuration).Configure(options);

        Assert.Equal(defaultProxies, options.KnownProxies.Order());
    }

    [Fact]
    public void RejectsAnInvalidConfiguredProxyAddress()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ForwardedHeaders:KnownProxies:0"] = "not-an-ip"
            })
            .Build();

        Assert.Throws<InvalidOperationException>(() =>
            new ForwardedHeadersOptionsSetup(configuration).Configure(new ForwardedHeadersOptions()));
    }

    [Theory]
    [InlineData("10.20.30.40", "https")]
    [InlineData("10.20.30.41", "http")]
    public async Task MiddlewareTrustsForwardedSchemeOnlyFromConfiguredProxy(string remoteAddress,
        string expectedScheme)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ForwardedHeaders:KnownProxies:0"] = "10.20.30.40"
            })
            .Build();
        var remoteIpAddress = IPAddress.Parse(remoteAddress);
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton<IConfiguration>(configuration);
        builder.Services.ConfigureOptions<ForwardedHeadersOptionsSetup>();

        await using var app = builder.Build();
        app.Use(next => context =>
        {
            context.Connection.RemoteIpAddress = remoteIpAddress;
            return next(context);
        });
        app.UseForwardedHeaders();
        app.Run(context => context.Response.WriteAsync(context.Request.Scheme));
        await app.StartAsync();

        using var client = app.GetTestClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.Add("X-Forwarded-Proto", "https");

        using var response = await client.SendAsync(request);
        var scheme = await response.Content.ReadAsStringAsync();

        Assert.Equal(expectedScheme, scheme);
    }
}
