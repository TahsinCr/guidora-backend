using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DataAccess.Concrete.Entityframework.Contexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace WebAPI.Services;

public sealed class ExpiredSessionCleanupService(
    IServiceScopeFactory scopeFactory, ILogger<ExpiredSessionCleanupService> logger) : BackgroundService
{
    private const int BatchSize = 500;
    private const int MaxBatchesPerCycle = 100;
    private static readonly TimeSpan Retention = TimeSpan.FromDays(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromDays(1));
        do
        {
            try
            {
                await PurgeAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Expired session cleanup failed; it will retry on the next cycle.");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task PurgeAsync(CancellationToken cancellationToken)
    {
        var cutoff = DateTime.UtcNow - Retention;
        for (var batchNumber = 0; batchNumber < MaxBatchesPerCycle && !cancellationToken.IsCancellationRequested;
             batchNumber++)
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<GuidoraDbContext>();
            var batch = db.UserSessions.Where(session => session.AbsoluteExpiresAt < cutoff)
                .OrderBy(session => session.AbsoluteExpiresAt).Select(session => session.Id)
                .Take(BatchSize);
            var deleted = await db.UserSessions.Where(session => batch.Contains(session.Id))
                .ExecuteDeleteAsync(cancellationToken);
            if (deleted < BatchSize) break;
        }
    }
}
