using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;
using System.Threading;
using System.Threading.Tasks;
using System;

namespace WebAPI.HealthChecks;

public sealed class RedisHealthCheck(IConnectionMultiplexer connectionMultiplexer) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await connectionMultiplexer.GetDatabase().PingAsync();
            return HealthCheckResult.Healthy();
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("Redis is unavailable.", exception);
        }
    }
}
