using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace WebAPI.Logging;

internal static class GuidoraHttpLoggingExtensions
{
    internal static IApplicationBuilder UseGuidoraHttpLogging(this IApplicationBuilder app) =>
        app.UseMiddleware<GuidoraHttpLoggingMiddleware>();
}

internal sealed class GuidoraHttpLoggingMiddleware(
    RequestDelegate next,
    ILogger<GuidoraHttpLoggingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var startedAt = Stopwatch.GetTimestamp();

        try
        {
            await next(context);
        }
        finally
        {
            logger.LogInformation(
                "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {ElapsedMilliseconds:0.000} ms (TraceId: {TraceId})",
                context.Request.Method,
                context.Request.Path.Value,
                context.Response.StatusCode,
                Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds,
                context.TraceIdentifier);
        }
    }
}
