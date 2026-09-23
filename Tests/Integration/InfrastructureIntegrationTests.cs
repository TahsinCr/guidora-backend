using Core.CrossCuttingConcerns.Messaging;
using Core.Entities.Concrete;
using DataAccess.Concrete.Entityframework;
using DataAccess.Concrete.Entityframework.Contexts;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;
using StackExchange.Redis;
using GuidoraRole = global::Core.Entities.Concrete.Role;

namespace Tests.Integration;

public sealed class InfrastructureIntegrationTests
{
    [RequiresEnvironmentFact("GUIDORA_TEST_CONNECTION_STRING")]
    public async Task PostgreSqlIsReachableAndHasNoPendingMigrations()
    {
        var options = new DbContextOptionsBuilder<GuidoraDbContext>()
            .UseNpgsql(Environment.GetEnvironmentVariable("GUIDORA_TEST_CONNECTION_STRING"))
            .Options;
        await using var context = new GuidoraDbContext(options);

        Assert.True(await context.Database.CanConnectAsync());
        Assert.Empty(await context.Database.GetPendingMigrationsAsync());
    }

    [RequiresEnvironmentFact("GUIDORA_TEST_CONNECTION_STRING")]
    public async Task EffectivePermissionsExcludeClaimsFromInactiveRoles()
    {
        var options = new DbContextOptionsBuilder<GuidoraDbContext>()
            .UseNpgsql(Environment.GetEnvironmentVariable("GUIDORA_TEST_CONNECTION_STRING"))
            .Options;
        await using var context = new GuidoraDbContext(options);
        await using var transaction = await context.Database.BeginTransactionAsync();

        var suffix = Guid.NewGuid().ToString("N");
        var user = new User
        {
            FirstName = "Integration",
            LastName = "User",
            Email = $"integration-{suffix}@example.test",
            PasswordHash = [1],
            PasswordSalt = [1],
            Status = true
        };
        var directClaim = new OperationClaim { Name = $"integration.direct.{suffix}" };
        var inactiveRoleClaim = new OperationClaim { Name = $"integration.inactive.{suffix}" };
        var activeRoleClaim = new OperationClaim { Name = $"integration.active.{suffix}" };
        var inactiveRole = new GuidoraRole { Name = $"Inactive-{suffix}", Code = $"inactive-{suffix}", IsActive = false };
        var activeRole = new GuidoraRole { Name = $"Active-{suffix}", Code = $"active-{suffix}", IsActive = true };

        context.Users.Add(user);
        context.OperationClaims.AddRange(directClaim, inactiveRoleClaim, activeRoleClaim);
        context.Roles.AddRange(inactiveRole, activeRole);
        await context.SaveChangesAsync();
        context.UserOperationClaims.Add(new UserOperationClaim { UserId = user.Id, OperationClaimId = directClaim.Id });
        context.UserRoles.AddRange(
            new UserRole { UserId = user.Id, RoleId = inactiveRole.Id },
            new UserRole { UserId = user.Id, RoleId = activeRole.Id });
        context.RoleOperationClaims.AddRange(
            new RoleOperationClaim { RoleId = inactiveRole.Id, OperationClaimId = inactiveRoleClaim.Id },
            new RoleOperationClaim { RoleId = activeRole.Id, OperationClaimId = activeRoleClaim.Id });
        await context.SaveChangesAsync();

        var claims = new EfUserDal(context).GetClaims(user).Select(claim => claim.Name).ToArray();

        Assert.Contains(directClaim.Name, claims);
        Assert.Contains(activeRoleClaim.Name, claims);
        Assert.DoesNotContain(inactiveRoleClaim.Name, claims);
        await transaction.RollbackAsync();
    }

    [RequiresEnvironmentFact("GUIDORA_TEST_REDIS_CONFIGURATION")]
    public async Task RedisRespondsAndUsesTheConfiguredLfuCachePolicy()
    {
        var options = ConfigurationOptions.Parse(Environment.GetEnvironmentVariable("GUIDORA_TEST_REDIS_CONFIGURATION")!);
        options.AbortOnConnectFail = false;
        await using var connection = await ConnectionMultiplexer.ConnectAsync(options);
        var endpoint = connection.GetEndPoints().Single();
        var server = connection.GetServer(endpoint);

        Assert.True((await connection.GetDatabase().PingAsync()) > TimeSpan.Zero);
        var policy = await server.ConfigGetAsync("maxmemory-policy");
        Assert.Contains(policy, pair => pair.Value == "allkeys-lfu");
        var persistence = await server.ConfigGetAsync("appendonly");
        Assert.Contains(persistence, pair => pair.Value == "no");
    }

    [RequiresEnvironmentFact("GUIDORA_TEST_RABBITMQ_HOST", "GUIDORA_TEST_RABBITMQ_PASSWORD")]
    public async Task RabbitMqAcceptsAnAuthenticatedChannel()
    {
        var factory = new ConnectionFactory
        {
            HostName = Environment.GetEnvironmentVariable("GUIDORA_TEST_RABBITMQ_HOST")!,
            Port = int.TryParse(Environment.GetEnvironmentVariable("GUIDORA_TEST_RABBITMQ_PORT"), out var port) ? port : 5672,
            UserName = Environment.GetEnvironmentVariable("GUIDORA_TEST_RABBITMQ_USERNAME") ?? "guidora",
            Password = Environment.GetEnvironmentVariable("GUIDORA_TEST_RABBITMQ_PASSWORD") ?? string.Empty,
            VirtualHost = Environment.GetEnvironmentVariable("GUIDORA_TEST_RABBITMQ_VIRTUAL_HOST") ?? "/"
        };

        await using var connection = await factory.CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();
        Assert.True(channel.IsOpen);
    }
}

public sealed class RequiresEnvironmentFactAttribute : FactAttribute
{
    public RequiresEnvironmentFactAttribute(params string[] environmentVariables)
    {
        var missing = environmentVariables
            .Where(variable => string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(variable)))
            .ToArray();
        if (missing.Length > 0)
        {
            Skip = $"Set {string.Join(" and ", missing)} to enable this infrastructure integration test.";
        }
    }
}
