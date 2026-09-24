using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Logging;
using WebAPI.Logging;

namespace Tests.WebAPI.Logging;

public sealed class HttpLoggingPipelineTests
{
    [Fact]
    public async Task LogsRequestMethodPathStatusAndDurationWithoutQueryOrBodies()
    {
        using var provider = new CapturingLoggerProvider();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders().AddProvider(provider);

        await using var app = builder.Build();
        app.UseGuidoraHttpLogging();
        app.Run(async context =>
        {
            context.Response.StatusCode = StatusCodes.Status202Accepted;
            await context.Response.WriteAsync("response-secret");
        });

        await app.StartAsync();
        using var client = app.GetTestClient();
        using var content = new StringContent("request-secret");

        using var response = await client.PostAsync("/api/auth/login?refreshToken=query-secret", content);

        Assert.Equal(StatusCodes.Status202Accepted, (int)response.StatusCode);
        var messages = string.Join('\n', provider.Messages);
        Assert.Contains("POST", messages);
        Assert.Contains("/api/auth/login", messages);
        Assert.Contains("202", messages);
        Assert.Contains("responded 202 in", messages);
        Assert.DoesNotContain("query-secret", messages);
        Assert.DoesNotContain("request-secret", messages);
        Assert.DoesNotContain("response-secret", messages);
    }

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        private readonly List<string> _messages = [];
        public IReadOnlyList<string> Messages => _messages;

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(this);

        public void Dispose() { }

        private sealed class CapturingLogger(CapturingLoggerProvider owner) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                if (IsEnabled(logLevel)) owner._messages.Add(formatter(state, exception));
            }
        }
    }
}
