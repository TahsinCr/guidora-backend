using Core.CrossCuttingConcerns.Messaging;
using Business.BusinessRules.Catalog;
using Business.Concrete;
using Core.Entities.Concrete;
using Core.Utilities.Results;
using DataAccess.Concrete.Entityframework;
using DataAccess.Concrete.Entityframework.Contexts;
using Entities;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;
using StackExchange.Redis;
using System.Text.Json;
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

    [RequiresEnvironmentFact("GUIDORA_TEST_CONNECTION_STRING")]
    public async Task GenericRepositoryListReadsDoNotTrackEntities()
    {
        var options = new DbContextOptionsBuilder<GuidoraDbContext>()
            .UseNpgsql(Environment.GetEnvironmentVariable("GUIDORA_TEST_CONNECTION_STRING"))
            .Options;
        await using var context = new GuidoraDbContext(options);
        var dal = new EfCategoryDal(context);

        _ = dal.GetAll();
        Assert.Empty(context.ChangeTracker.Entries());

        _ = await dal.GetAllAsync();

        Assert.Empty(context.ChangeTracker.Entries());
    }

    [RequiresEnvironmentFact("GUIDORA_TEST_CONNECTION_STRING")]
    public async Task CatalogEntityUpdatesRejectAStalePostgreSqlXminVersion()
    {
        var connectionString = Environment.GetEnvironmentVariable("GUIDORA_TEST_CONNECTION_STRING");
        var options = new DbContextOptionsBuilder<GuidoraDbContext>().UseNpgsql(connectionString).Options;
        var slug = $"xmin-{Guid.NewGuid():N}";
        int categoryId;

        await using (var setup = new GuidoraDbContext(options))
        {
            var category = new global::Entities.Category
            {
                CategoryName = slug,
                Slug = slug,
                Description = "Concurrency integration test",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            setup.Categories.Add(category);
            await setup.SaveChangesAsync();
            categoryId = category.CategoryId;
        }

        try
        {
            await using var first = new GuidoraDbContext(options);
            await using var second = new GuidoraDbContext(options);
            var firstCategory = await first.Categories.SingleAsync(item => item.CategoryId == categoryId);
            var secondCategory = await second.Categories.SingleAsync(item => item.CategoryId == categoryId);

            firstCategory.Description = "first writer";
            firstCategory.UpdatedAt = DateTime.UtcNow;
            await first.SaveChangesAsync();

            secondCategory.Description = "stale writer";
            secondCategory.UpdatedAt = DateTime.UtcNow.AddSeconds(1);
            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
        }
        finally
        {
            await using var cleanup = new GuidoraDbContext(options);
            var category = await cleanup.Categories.SingleOrDefaultAsync(item => item.CategoryId == categoryId);
            if (category is not null)
            {
                cleanup.Categories.Remove(category);
                await cleanup.SaveChangesAsync();
            }
        }
    }

    [RequiresEnvironmentFact("GUIDORA_TEST_CONNECTION_STRING")]
    public async Task AuditRevertRestoresProductAndCategoryAndAppendsLinkedAuditEntries()
    {
        var options = new DbContextOptionsBuilder<GuidoraDbContext>()
            .UseNpgsql(Environment.GetEnvironmentVariable("GUIDORA_TEST_CONNECTION_STRING"))
            .Options;
        await using var context = new GuidoraDbContext(options);
        await using var transaction = await context.Database.BeginTransactionAsync();

        var suffix = Guid.NewGuid().ToString("N");
        var user = new User
        {
            FirstName = "Audit", LastName = "Revert", Email = $"audit-{suffix}@example.test",
            PasswordHash = [1], PasswordSalt = [1], Status = true
        };
        var category = new global::Entities.Category
        {
            CategoryName = $"Before-{suffix}", Slug = $"before-{suffix}", Description = "before",
            IsActive = true, CreatedAt = DateTime.UtcNow.AddDays(-1)
        };
        context.Users.Add(user);
        context.Categories.Add(category);
        await context.SaveChangesAsync();

        var published = await context.ProductStatuses.SingleAsync(status => status.Code == "published");
        var product = new Product
        {
            Name = $"Product-Before-{suffix}", Slug = $"product-before-{suffix}",
            ShortDescription = "before summary", Description = "before details",
            WebsiteUrl = $"https://example.test/{suffix}", LogoUrl = "https://example.test/logo.png",
            ProductStatusId = published.Id, CreatedByUserId = user.Id, CreatedAt = DateTime.UtcNow.AddDays(-1)
        };
        context.Products.Add(product);
        await context.SaveChangesAsync();
        context.ProductCategories.Add(new ProductCategory { ProductId = product.ProductId, CategoryId = category.CategoryId });
        await context.SaveChangesAsync();

        var productOldUpdatedAt = DateTime.UtcNow.AddMinutes(-2);
        var productNewUpdatedAt = DateTime.UtcNow.AddMinutes(-1);
        product.UpdatedAt = productNewUpdatedAt;
        product.Name = $"Product-After-{suffix}";
        product.Slug = $"product-after-{suffix}";
        product.ShortDescription = "after summary";
        product.Description = "after details";
        product.WebsiteUrl = $"https://example.test/after-{suffix}";
        await context.SaveChangesAsync();
        var productAudit = new AuditLog
        {
            ActorUserId = user.Id, EntityType = nameof(Product), EntityId = product.ProductId.ToString(),
            Action = "Updated", CreatedAt = DateTime.UtcNow.AddSeconds(-10),
            OldValues = JsonSerializer.Serialize(new
            {
                Name = $"Product-Before-{suffix}", Slug = $"product-before-{suffix}",
                ShortDescription = "before summary", Description = "before details",
                WebsiteUrl = $"https://example.test/{suffix}", LogoUrl = "https://example.test/logo.png",
                UpdatedAt = productOldUpdatedAt, CategoryIds = new[] { category.CategoryId }, TagIds = Array.Empty<int>()
            }),
            NewValues = JsonSerializer.Serialize(new
            {
                product.Name, product.Slug, product.ShortDescription, product.Description,
                product.WebsiteUrl, product.LogoUrl, product.UpdatedAt,
                CategoryIds = new[] { category.CategoryId }, TagIds = Array.Empty<int>()
            })
        };
        context.AuditLogs.Add(productAudit);

        var categoryOld = new { category.CategoryName, category.Slug, category.Description, category.IsActive, category.UpdatedAt };
        category.CategoryName = $"After-{suffix}";
        category.Slug = $"after-{suffix}";
        category.Description = "after";
        category.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();
        var categoryAudit = new AuditLog
        {
            ActorUserId = user.Id, EntityType = nameof(global::Entities.Category),
            EntityId = category.CategoryId.ToString(), Action = "Updated", CreatedAt = DateTime.UtcNow,
            OldValues = JsonSerializer.Serialize(categoryOld),
            NewValues = JsonSerializer.Serialize(new
            {
                category.CategoryName, category.Slug, category.Description, category.IsActive, category.UpdatedAt
            })
        };
        context.AuditLogs.Add(categoryAudit);
        await context.SaveChangesAsync();

        var productDal = new EfProductDal(context);
        var categoryDal = new EfCategoryDal(context);
        var revertManager = new AuditRevertManager(new EfAuditLogDal(context), productDal,
            new EfProductStatusDal(context), new EfProductCategoryDal(context), new EfProductTagDal(context),
            categoryDal, new ProductBusinessRules(productDal, categoryDal), new CategoryBusinessRules(categoryDal),
            new EfTagDal(context), new EfTagCategoryDal(context), new TagBusinessRules(),
            new GuidoraUnitOfWork(context));

        var productResult = await revertManager.RevertAsync(productAudit.Id, user.Id);
        var categoryResult = await revertManager.RevertAsync(categoryAudit.Id, user.Id);

        Assert.True(productResult.Success, productResult.Message);
        Assert.True(categoryResult.Success, categoryResult.Message);
        Assert.Equal($"Product-Before-{suffix}", product.Name);
        Assert.Equal($"Before-{suffix}", category.CategoryName);
        Assert.Equal($"before-{suffix}", category.Slug);
        Assert.Contains(context.AuditLogs, entry => entry.RevertsAuditLogId == productAudit.Id && entry.Action == "Reverted");
        Assert.Contains(context.AuditLogs, entry => entry.RevertsAuditLogId == categoryAudit.Id && entry.Action == "Reverted");
        await transaction.RollbackAsync();
    }

    [RequiresEnvironmentFact("GUIDORA_TEST_CONNECTION_STRING")]
    public async Task AuditRevertRejectsMismatchedCurrentStateWithoutMutation()
    {
        var options = new DbContextOptionsBuilder<GuidoraDbContext>()
            .UseNpgsql(Environment.GetEnvironmentVariable("GUIDORA_TEST_CONNECTION_STRING"))
            .Options;
        await using var context = new GuidoraDbContext(options);
        await using var transaction = await context.Database.BeginTransactionAsync();
        var suffix = Guid.NewGuid().ToString("N");
        var user = new User
        {
            FirstName = "Audit", LastName = "Conflict", Email = $"conflict-{suffix}@example.test",
            PasswordHash = [1], PasswordSalt = [1], Status = true
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();
        var status = await context.ProductStatuses.SingleAsync(item => item.Code == "published");
        var product = new Product
        {
            Name = $"Current-{suffix}", Slug = $"current-{suffix}", ShortDescription = "summary",
            Description = "details", WebsiteUrl = $"https://example.test/{suffix}",
            LogoUrl = "https://example.test/logo.png", ProductStatusId = status.Id,
            CreatedByUserId = user.Id, CreatedAt = DateTime.UtcNow
        };
        context.Products.Add(product);
        await context.SaveChangesAsync();
        var audit = new AuditLog
        {
            ActorUserId = user.Id, EntityType = nameof(Product), EntityId = product.ProductId.ToString(),
            Action = "Updated", CreatedAt = DateTime.UtcNow,
            OldValues = "{}",
            NewValues = JsonSerializer.Serialize(new { Name = $"Unobserved-{suffix}" })
        };
        context.AuditLogs.Add(audit);
        await context.SaveChangesAsync();

        var productDal = new EfProductDal(context);
        var categoryDal = new EfCategoryDal(context);
        var revertManager = new AuditRevertManager(new EfAuditLogDal(context), productDal,
            new EfProductStatusDal(context), new EfProductCategoryDal(context), new EfProductTagDal(context),
            categoryDal, new ProductBusinessRules(productDal, categoryDal), new CategoryBusinessRules(categoryDal),
            new EfTagDal(context), new EfTagCategoryDal(context), new TagBusinessRules(),
            new GuidoraUnitOfWork(context));

        var result = await revertManager.RevertAsync(audit.Id, user.Id);

        Assert.IsType<ConflictResult>(result);
        Assert.Equal($"Current-{suffix}", product.Name);
        Assert.False(await context.AuditLogs.AnyAsync(entry => entry.RevertsAuditLogId == audit.Id));
        await transaction.RollbackAsync();
    }

    [RequiresEnvironmentFact("GUIDORA_TEST_REDIS_CONFIGURATION")]
    public async Task RedisRespondsAndUsesTheConfiguredLfuCachePolicy()
    {
        var options = ConfigurationOptions.Parse(Environment.GetEnvironmentVariable("GUIDORA_TEST_REDIS_CONFIGURATION")!);
        options.AbortOnConnectFail = false;
        options.AllowAdmin = true;
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
