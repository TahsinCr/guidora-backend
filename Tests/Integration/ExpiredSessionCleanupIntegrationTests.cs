using Npgsql;
using WebAPI.Services;

namespace Tests.Integration;

public sealed class ExpiredSessionCleanupIntegrationTests
{
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
}
