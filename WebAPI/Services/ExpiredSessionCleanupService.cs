using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Data.Common;
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
    internal const long CleanupLockKey = 0x53434C4E00000001L;
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

    internal async Task PurgeAsync(CancellationToken cancellationToken, DateTime? utcNow = null)
    {
        var cutoff = (utcNow ?? DateTime.UtcNow) - Retention;
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GuidoraDbContext>();
        await db.Database.OpenConnectionAsync(cancellationToken);
        var connection = db.Database.GetDbConnection();
        var acquiredLock = false;
        try
        {
            acquiredLock = await TryAcquireLockAsync(connection, cancellationToken);
            if (!acquiredLock)
            {
                logger.LogDebug("Expired session cleanup is already running on another backend instance.");
                return;
            }

            var totalDeleted = 0;
            var batchesExecuted = 0;
            for (; batchesExecuted < MaxBatchesPerCycle && !cancellationToken.IsCancellationRequested;
                 batchesExecuted++)
            {
                var batch = db.UserSessions.Where(session => session.AbsoluteExpiresAt < cutoff)
                    .OrderBy(session => session.AbsoluteExpiresAt).Select(session => session.Id)
                    .Take(BatchSize);
                var deleted = await db.UserSessions.Where(session => batch.Contains(session.Id))
                    .ExecuteDeleteAsync(cancellationToken);
                totalDeleted += deleted;
                if (deleted < BatchSize)
                {
                    batchesExecuted++;
                    break;
                }
            }

            if (totalDeleted > 0)
                logger.LogInformation("Expired session cleanup deleted {DeletedSessionCount} sessions in {BatchCount} batches.",
                    totalDeleted, batchesExecuted);
            if (batchesExecuted == MaxBatchesPerCycle)
                logger.LogWarning("Expired session cleanup reached its per-cycle limit of {BatchLimit} batches.",
                    MaxBatchesPerCycle);
        }
        finally
        {
            if (acquiredLock)
            {
                try
                {
                    if (!await ReleaseLockAsync(connection))
                        logger.LogWarning("Expired session cleanup advisory lock was not held during release.");
                }
                catch (Exception exception)
                {
                    logger.LogError(exception, "Failed to release the expired session cleanup advisory lock.");
                }
            }

            await db.Database.CloseConnectionAsync();
        }
    }

    private static async Task<bool> TryAcquireLockAsync(DbConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT pg_try_advisory_lock(@lockKey)";
        AddLockKeyParameter(command);
        return await command.ExecuteScalarAsync(cancellationToken) is true;
    }

    private static async Task<bool> ReleaseLockAsync(DbConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT pg_advisory_unlock(@lockKey)";
        AddLockKeyParameter(command);
        return await command.ExecuteScalarAsync(CancellationToken.None) is true;
    }

    private static void AddLockKeyParameter(DbCommand command)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = "lockKey";
        parameter.Value = CleanupLockKey;
        command.Parameters.Add(parameter);
    }
}
