using Npgsql;
using Core.Entities.Concrete;
using DataAccess.Concrete.Entityframework.Contexts;
using Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using WebAPI.Services;

namespace Tests.Integration;

public sealed class ExpiredSessionCleanupIntegrationTests
{
    [RequiresEnvironmentFact("GUIDORA_TEST_CONNECTION_STRING")]
    public async Task CleanupDeletesOnlySessionsPastAbsoluteExpiryRetention()
    {
        var options = new DbContextOptionsBuilder<GuidoraDbContext>()
            .UseNpgsql(Environment.GetEnvironmentVariable("GUIDORA_TEST_CONNECTION_STRING"))
            .Options;
        var now = DateTime.UtcNow;
        int userId;
        var expiredId = Guid.NewGuid();
        var retainedId = Guid.NewGuid();
        var activeId = Guid.NewGuid();

        await using (var setup = new GuidoraDbContext(options))
        {
            var user = new User
            {
                FirstName = "Cleanup", LastName = "Integration",
                Email = $"cleanup-{Guid.NewGuid():N}@example.test",
                PasswordHash = [1], PasswordSalt = [1], Status = true
            };
            setup.Users.Add(user);
            await setup.SaveChangesAsync();
            userId = user.Id;
            setup.UserSessions.AddRange(
                CreateSession(expiredId, userId, now.AddDays(-31)),
                CreateSession(retainedId, userId, now.AddDays(-29)),
                CreateSession(activeId, userId, now.AddDays(1)));
            await setup.SaveChangesAsync();
        }

        try
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddDbContext<GuidoraDbContext>(builder => builder.UseNpgsql(
                Environment.GetEnvironmentVariable("GUIDORA_TEST_CONNECTION_STRING")));
            await using var provider = services.BuildServiceProvider();
            var cleanup = new ExpiredSessionCleanupService(provider.GetRequiredService<IServiceScopeFactory>(),
                NullLogger<ExpiredSessionCleanupService>.Instance);

            await cleanup.PurgeAsync(CancellationToken.None, now);

            await using var verify = new GuidoraDbContext(options);
            var remainingIds = await verify.UserSessions.Where(session => session.UserId == userId)
                .Select(session => session.Id).ToArrayAsync();
            Assert.DoesNotContain(expiredId, remainingIds);
            Assert.Contains(retainedId, remainingIds);
            Assert.Contains(activeId, remainingIds);
        }
        finally
        {
            await using var cleanup = new GuidoraDbContext(options);
            var user = await cleanup.Users.SingleOrDefaultAsync(item => item.Id == userId);
            if (user is not null)
            {
                cleanup.Users.Remove(user);
                await cleanup.SaveChangesAsync();
            }
        }
    }

    [RequiresEnvironmentFact("GUIDORA_TEST_CONNECTION_STRING")]
    public async Task CleanupAdvisoryLockAllowsOnlyOneBackendInstanceAtATime()
    {
        var connectionString = Environment.GetEnvironmentVariable("GUIDORA_TEST_CONNECTION_STRING")!;
        await using var firstConnection = new NpgsqlConnection(connectionString);
        await using var secondConnection = new NpgsqlConnection(connectionString);
        await firstConnection.OpenAsync();
        await secondConnection.OpenAsync();

        try
        {
            Assert.True(await TryAcquireAsync(firstConnection));
            Assert.False(await TryAcquireAsync(secondConnection));
            Assert.True(await ReleaseAsync(firstConnection));
            Assert.True(await TryAcquireAsync(secondConnection));
        }
        finally
        {
            await ReleaseAsync(firstConnection);
            await ReleaseAsync(secondConnection);
        }
    }

    private static async Task<bool> TryAcquireAsync(NpgsqlConnection connection) =>
        await ExecuteBooleanAsync(connection, "SELECT pg_try_advisory_lock(@lockKey)");

    private static async Task<bool> ReleaseAsync(NpgsqlConnection connection) =>
        await ExecuteBooleanAsync(connection, "SELECT pg_advisory_unlock(@lockKey)");

    private static async Task<bool> ExecuteBooleanAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("lockKey", ExpiredSessionCleanupService.CleanupLockKey);
        return (bool)(await command.ExecuteScalarAsync())!;
    }

    private static UserSession CreateSession(Guid id, int userId, DateTime absoluteExpiry) => new()
    {
        Id = id,
        SessionFamilyId = id,
        UserId = userId,
        RefreshTokenHash = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)),
        CreatedAt = DateTime.UtcNow,
        ExpiresAt = absoluteExpiry,
        AbsoluteExpiresAt = absoluteExpiry,
        DeviceName = "cleanup integration test"
    };
}
