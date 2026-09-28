using Core.CrossCuttingConcerns.Messaging;
using Core.CrossCuttingConcerns.Caching.Redis;
using Core.CrossCuttingConcerns.Caching;
using Business.BusinessRules.Catalog;
using Business.Concrete;
using Core.Entities.Concrete;
using Core.Utilities.Results;
using Core.Exceptions;
using DataAccess.Concrete.Entityframework;
using DataAccess.Concrete.Entityframework.Contexts;
using Core.DataAccess;
using Tests.Core.TestDoubles;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using Entities;
using Entities.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using RabbitMQ.Client;
using StackExchange.Redis;
using System.Reflection;
using System.Text.Json;
using GuidoraRole = global::Core.Entities.Concrete.Role;

namespace Tests.Integration;

public sealed class InfrastructureIntegrationTests
{
    [RequiresEnvironmentFact("GUIDORA_TEST_CONNECTION_STRING")]
    public async Task SessionInvalidationRollsBackAuthorizationIncrementWhenSessionSaveFails()
    {
        var options = new DbContextOptionsBuilder<GuidoraDbContext>()
            .UseNpgsql(Environment.GetEnvironmentVariable("GUIDORA_TEST_CONNECTION_STRING"))
            .Options;
        int userId;
        await using (var setup = new GuidoraDbContext(options))
        {
            var user = new User
            {
                FirstName = "Rollback", LastName = "Check",
                Email = $"rollback-{Guid.NewGuid():N}@example.test",
                PasswordHash = [1], PasswordSalt = [1], Status = true
            };
            setup.Users.Add(user);
            await setup.SaveChangesAsync();
            userId = user.Id;
            setup.UserAuthenticationStates.Add(new UserAuthenticationState
                { UserId = userId, AuthorizationVersion = 5, UpdatedAt = DateTime.UtcNow });
            await setup.SaveChangesAsync();
        }

        try
        {
            await using (var context = new GuidoraDbContext(options))
            {
                var manager = new UserSessionInvalidationManager(
                    new EfUserAuthenticationStateDal(context), new EfUserSessionDal(context),
                    new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())),
                    new FailingSaveUnitOfWork(), Microsoft.Extensions.Logging.Abstractions.NullLogger<global::Business.Concrete.UserSessionInvalidationManager>.Instance);
                await Assert.ThrowsAsync<InvalidOperationException>(() =>
                    manager.InvalidateUserSessionsAsync(userId));
            }

            await using var verification = new GuidoraDbContext(options);
            Assert.Equal(5, await verification.UserAuthenticationStates
                .Where(item => item.UserId == userId)
                .Select(item => item.AuthorizationVersion).SingleAsync());
        }
        finally
        {
            await using var cleanup = new GuidoraDbContext(options);
            cleanup.Users.Remove(await cleanup.Users.SingleAsync(item => item.Id == userId));
            await cleanup.SaveChangesAsync();
        }
    }

    private sealed class FailingSaveUnitOfWork : IUnitOfWork
    {
        public int SaveChanges() => throw new InvalidOperationException("Simulated save failure.");
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
            Task.FromException<int>(new InvalidOperationException("Simulated save failure."));
        public Task AcquireTransactionLockAsync(long key, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    [RequiresEnvironmentFact("GUIDORA_TEST_CONNECTION_STRING")]
    public async Task ConcurrentAuthorizationInvalidationsCannotReuseAnIntermediateTokenVersion()
    {
        var options = new DbContextOptionsBuilder<GuidoraDbContext>()
            .UseNpgsql(Environment.GetEnvironmentVariable("GUIDORA_TEST_CONNECTION_STRING"))
            .Options;
        int userId;
        await using (var setup = new GuidoraDbContext(options))
        {
            var user = new User
            {
                FirstName = "Version", LastName = "Race",
                Email = $"version-race-{Guid.NewGuid():N}@example.test",
                PasswordHash = [1], PasswordSalt = [1], Status = true
            };
            setup.Users.Add(user);
            await setup.SaveChangesAsync();
            userId = user.Id;
            setup.UserAuthenticationStates.Add(new UserAuthenticationState
                { UserId = userId, AuthorizationVersion = 5, UpdatedAt = DateTime.UtcNow });
            await setup.SaveChangesAsync();
        }

        try
        {
            async Task IncrementAsync()
            {
                await using var context = new GuidoraDbContext(options);
                await new EfUserAuthenticationStateDal(context)
                    .IncrementAuthorizationVersionsAsync([userId], DateTime.UtcNow);
            }

            await Task.WhenAll(IncrementAsync(), IncrementAsync());
            await using var verification = new GuidoraDbContext(options);
            var version = await verification.UserAuthenticationStates
                .Where(item => item.UserId == userId)
                .Select(item => item.AuthorizationVersion).SingleAsync();
            Assert.Equal(7, version);
            Assert.NotEqual(6, version); // A token issued between the two commits must now be invalid.
        }
        finally
        {
            await using var cleanup = new GuidoraDbContext(options);
            var user = await cleanup.Users.SingleAsync(item => item.Id == userId);
            cleanup.Users.Remove(user);
            await cleanup.SaveChangesAsync();
        }
    }

    [RequiresEnvironmentFact("GUIDORA_TEST_CONNECTION_STRING")]
    public async Task ProductIdentityRuleUsesCitextForCaseInsensitiveNameAndSlug()
    {
        var options = new DbContextOptionsBuilder<GuidoraDbContext>()
            .UseNpgsql(Environment.GetEnvironmentVariable("GUIDORA_TEST_CONNECTION_STRING"))
            .Options;
        await using var context = new GuidoraDbContext(options);
        await using var transaction = await context.Database.BeginTransactionAsync();
        var suffix = Guid.NewGuid().ToString("N");
        var user = new User
        {
            FirstName = "Identity", LastName = "Check", Email = $"identity-{suffix}@example.test",
            PasswordHash = [1], PasswordSalt = [1], Status = true
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();
        context.Products.Add(new Product
        {
            Name = $"Guidora {suffix}", Slug = $"guidora-{suffix}",
            WebsiteUrl = $"https://identity-{suffix}.example.test",
            ShortDescription = "Identity check", Description = "Case insensitive identity.",
            LogoUrl = "https://example.test/logo.png", CreatedByUserId = user.Id,
            ProductStatusId = 1, CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var rules = new ProductBusinessRules(new EfProductDal(context), new EfCategoryDal(context));
        Assert.IsType<ConflictResult>(await rules.EnsureIdentityIsAvailableAsync(
            $"gUIDORA {suffix}", $"different-{suffix}", $"https://other-{suffix}.example.test"));
        Assert.IsType<ConflictResult>(await rules.EnsureIdentityIsAvailableAsync(
            $"Other {suffix}", $"GUIDORA-{suffix}", $"https://other-{suffix}.example.test"));
        await transaction.RollbackAsync();
    }

    [RequiresEnvironmentFact("GUIDORA_TEST_CONNECTION_STRING")]
    public async Task CommunityVerificationRequiresThirtyEligibleVotesAndModeratorApproval()
    {
        var options = new DbContextOptionsBuilder<GuidoraDbContext>()
            .UseNpgsql(Environment.GetEnvironmentVariable("GUIDORA_TEST_CONNECTION_STRING"))
            .Options;
        await using var context = new GuidoraDbContext(options);
        await using var transaction = await context.Database.BeginTransactionAsync();
        var suffix = Guid.NewGuid().ToString("N");
        var users = Enumerable.Range(0, 33).Select(index => new User
        {
            FirstName = "Community", LastName = $"Vote{index}",
            Email = $"vote-{index}-{suffix}@example.test",
            PasswordHash = [1], PasswordSalt = [1], Status = true,
            CreatedAt = DateTime.UtcNow.AddDays(-8), EmailVerifiedAt = DateTime.UtcNow.AddDays(-7)
        }).ToArray();
        context.Users.AddRange(users);
        await context.SaveChangesAsync();
        var role = await context.Roles.SingleAsync(item => item.Code == "user");
        context.UserRoles.AddRange(users.Skip(1).Select(user => new UserRole
            { UserId = user.Id, RoleId = role.Id }));
        var moderatorRole = await context.Roles.SingleAsync(item => item.Code == "moderator");
        context.UserRoles.Add(new UserRole { UserId = users[32].Id, RoleId = moderatorRole.Id });
        var product = new Product
        {
            Name = $"Community {suffix}", Slug = $"community-{suffix}",
            ShortDescription = "Community voting test", Description = "Verification vote workflow.",
            WebsiteUrl = $"https://community-{suffix}.example.test",
            LogoUrl = "https://example.test/logo.png", CreatedByUserId = users[0].Id,
            ProductStatusId = 1, CreatedAt = DateTime.UtcNow
        };
        context.Products.Add(product);
        await context.SaveChangesAsync();

        var manager = new CommunityVerificationManager(new EfProductDal(context), new EfProductStatusDal(context),
            new EfUserDal(context),
            new EfRoleDal(context), new EfUserRoleDal(context), new EfProductVerificationVoteDal(context),
            new EfProductVerificationDal(context), new EfCommunityVerificationReviewDal(context),
            new EfVerificationTypeDal(context),
            new EfAuditLogDal(context), new GuidoraUnitOfWork(context));
        Assert.IsAssignableFrom<IForbiddenResult>(await manager.VoteAsync(product.ProductId, users[0].Id,
            new CommunityVerificationVoteRequest { Approved = true }));
        users[32].EmailVerifiedAt = null;
        await context.SaveChangesAsync();
        Assert.IsAssignableFrom<IForbiddenResult>(await manager.VoteAsync(product.ProductId, users[32].Id,
            new CommunityVerificationVoteRequest { Approved = true }));
        users[32].EmailVerifiedAt = DateTime.UtcNow;
        users[32].CreatedAt = DateTime.UtcNow.AddDays(-6);
        await context.SaveChangesAsync();
        Assert.IsAssignableFrom<IForbiddenResult>(await manager.VoteAsync(product.ProductId, users[32].Id,
            new CommunityVerificationVoteRequest { Approved = true }));
        var hiddenStatus = await context.ProductStatuses.SingleAsync(item => item.Code == "hidden");
        product.ProductStatusId = hiddenStatus.Id;
        await context.SaveChangesAsync();
        Assert.IsType<ConflictResult>(await manager.VoteAsync(product.ProductId, users[1].Id,
            new CommunityVerificationVoteRequest { Approved = true }));
        product.ProductStatusId = 1;
        await context.SaveChangesAsync();
        Assert.True((await manager.VoteAsync(product.ProductId, users[1].Id,
            new CommunityVerificationVoteRequest { Approved = false })).Success);
        Assert.IsType<ConflictResult>(await manager.VoteAsync(product.ProductId, users[1].Id,
            new CommunityVerificationVoteRequest { Approved = true }));

        foreach (var voter in users.Skip(2).Take(30))
            Assert.True((await manager.VoteAsync(product.ProductId, voter.Id,
                new CommunityVerificationVoteRequest { Approved = true }, originIpHash: "shared-ip-signal",
                originClientHash: "shared-client-signal")).Success);

        var communityType = await context.VerificationTypes.SingleAsync(item => item.Code == "community");
        Assert.False(await context.ProductVerifications.AnyAsync(item => item.ProductId == product.ProductId &&
            item.VerificationTypeId == communityType.Id));
        Assert.Equal("pending", (await context.CommunityVerificationReviews.SingleAsync(item =>
            item.ProductId == product.ProductId)).Status);
        var pending = await manager.GetPendingAsync(new PageRequest { Page = 1, PageSize = 100 });
        var reviewItem = Assert.Single(pending.Data.Items, item => item.ProductId == product.ProductId);
        Assert.Equal(30, reviewItem.PositiveVotes);
        Assert.Equal(1, reviewItem.NegativeVotes);
        Assert.Equal(30, reviewItem.CurrentlyEligiblePositiveVotes);
        Assert.Equal(1, reviewItem.DistinctIpSignals);
        Assert.Equal(1, reviewItem.DistinctClientSignals);
        users[2].Status = false;
        await context.SaveChangesAsync();
        var summary = await new ProductVerificationManager(new EfProductDal(context),
            new EfProductStatusDal(context), new EfVerificationTypeDal(context),
            new EfProductVerificationDal(context), new EfProductVerificationVoteDal(context),
            new EfCommunityVerificationReviewDal(context), new EfAuditLogDal(context),
            new GuidoraUnitOfWork(context)).GetSummaryAsync(product.ProductId, null);
        Assert.Equal(29, summary.Data.PositiveVotes);
        Assert.IsType<ConflictResult>(await manager.ApproveAsync(product.ProductId, users[32].Id,
            new ReviewNoteRequest { Note = "One voter is inactive." }));
        users[2].Status = true;
        await context.SaveChangesAsync();
        Assert.True((await manager.ApproveAsync(product.ProductId, users[32].Id,
            new ReviewNoteRequest { Note = "Reviewed voter signals." })).Success);
        var verification = await context.ProductVerifications.SingleAsync(item =>
            item.ProductId == product.ProductId && item.VerificationTypeId == communityType.Id);
        Assert.Null(verification.RevokedAt);
        Assert.Equal(30, await context.ProductVerificationVotes.CountAsync(item =>
            item.ProductId == product.ProductId && item.Approved));

        Assert.True((await manager.RevokeAsync(product.ProductId, users[1].Id,
            new ReviewNoteRequest { Note = "Invalid community evidence." })).Success);
        Assert.NotNull(verification.RevokedAt);
        Assert.IsType<ConflictResult>(await manager.VoteAsync(product.ProductId, users[7].Id,
            new CommunityVerificationVoteRequest { Approved = true }));
        await transaction.RollbackAsync();
    }

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

        var claims = (await new EfUserDal(context).GetClaimsAsync(user)).Select(claim => claim.Name).ToArray();

        Assert.Contains(directClaim.Name, claims);
        Assert.Contains(activeRoleClaim.Name, claims);
        Assert.DoesNotContain(inactiveRoleClaim.Name, claims);
        await transaction.RollbackAsync();
    }

    [RequiresEnvironmentFact("GUIDORA_TEST_CONNECTION_STRING")]
    public async Task CommunityUserReceivesProductUpdatePermissionFromSeededRole()
    {
        var options = new DbContextOptionsBuilder<GuidoraDbContext>()
            .UseNpgsql(Environment.GetEnvironmentVariable("GUIDORA_TEST_CONNECTION_STRING"))
            .Options;
        await using var context = new GuidoraDbContext(options);
        await using var transaction = await context.Database.BeginTransactionAsync();

        var role = await context.Roles.SingleAsync(item => item.Code == "user" && item.IsActive);
        var updateClaim = await context.OperationClaims.SingleAsync(item => item.Name == "Product.Update");
        Assert.True(await context.RoleOperationClaims.AnyAsync(assignment =>
            assignment.RoleId == role.Id && assignment.OperationClaimId == updateClaim.Id));

        var suffix = Guid.NewGuid().ToString("N");
        var user = new User
        {
            FirstName = "Integration",
            LastName = "Community",
            Email = $"community-update-{suffix}@example.test",
            PasswordHash = [1],
            PasswordSalt = [1],
            Status = true
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();
        context.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id });
        await context.SaveChangesAsync();

        var effectiveClaims = await new EfUserDal(context).GetClaimsAsync(user);

        Assert.Contains(effectiveClaims, claim => claim.Name == "Product.Update");
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
    public async Task ProductTagDalDetectsOnlyExclusiveChangesThatWouldBreakCurrentSelections()
    {
        var options = new DbContextOptionsBuilder<GuidoraDbContext>()
            .UseNpgsql(Environment.GetEnvironmentVariable("GUIDORA_TEST_CONNECTION_STRING"))
            .Options;
        await using var context = new GuidoraDbContext(options);
        await using var transaction = await context.Database.BeginTransactionAsync();
        var suffix = Guid.NewGuid().ToString("N");
        var user = new User
        {
            FirstName = "Tag",
            LastName = "Invariant",
            Email = $"tag-invariant-{suffix}@example.test",
            PasswordHash = [1],
            PasswordSalt = [1],
            Status = true
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var conflictingCategory = new TagCategory
        {
            Name = $"Conflicting-{suffix}", Slug = $"conflicting-{suffix}", AllowsMultipleSelection = true,
            IsActive = true
        };
        var safeCategory = new TagCategory
        {
            Name = $"Safe-{suffix}", Slug = $"safe-{suffix}", AllowsMultipleSelection = true, IsActive = true
        };
        context.TagCategories.AddRange(conflictingCategory, safeCategory);
        await context.SaveChangesAsync();

        var targetTag = new Tag
        {
            TagCategoryId = conflictingCategory.Id, Name = $"Target-{suffix}", Slug = $"target-{suffix}",
            IsActive = true
        };
        var siblingTag = new Tag
        {
            TagCategoryId = conflictingCategory.Id, Name = $"Sibling-{suffix}", Slug = $"sibling-{suffix}",
            IsActive = true
        };
        var soloTag = new Tag
        {
            TagCategoryId = safeCategory.Id, Name = $"Solo-{suffix}", Slug = $"solo-{suffix}", IsActive = true
        };
        context.Tags.AddRange(targetTag, siblingTag, soloTag);
        var firstProduct = new Product
        {
            Name = $"Conflicting product {suffix}", Slug = $"conflicting-product-{suffix}",
            ShortDescription = "Tag invariant", Description = "Integration query test",
            WebsiteUrl = $"https://tag-invariant-{suffix}.example.test/conflicting",
            LogoUrl = "https://example.test/logo.png", ProductStatusId = 1, CreatedByUserId = user.Id,
            CreatedAt = DateTime.UtcNow
        };
        var secondProduct = new Product
        {
            Name = $"Safe product {suffix}", Slug = $"safe-product-{suffix}",
            ShortDescription = "Tag invariant", Description = "Integration query test",
            WebsiteUrl = $"https://tag-invariant-{suffix}.example.test/safe",
            LogoUrl = "https://example.test/logo.png", ProductStatusId = 1, CreatedByUserId = user.Id,
            CreatedAt = DateTime.UtcNow
        };
        context.Products.AddRange(firstProduct, secondProduct);
        await context.SaveChangesAsync();
        context.ProductTags.AddRange(
            new ProductTag { ProductId = firstProduct.ProductId, TagId = targetTag.Id },
            new ProductTag { ProductId = firstProduct.ProductId, TagId = siblingTag.Id },
            new ProductTag { ProductId = secondProduct.ProductId, TagId = soloTag.Id });
        await context.SaveChangesAsync();

        var dal = new EfProductTagDal(context);
        Assert.True(await dal.HasOtherTagInSameCategoryForAssignedProductsAsync(
            targetTag.Id, conflictingCategory.Id));
        Assert.False(await dal.HasOtherTagInSameCategoryForAssignedProductsAsync(
            soloTag.Id, safeCategory.Id));

        await transaction.RollbackAsync();
    }

    [RequiresEnvironmentFact("GUIDORA_TEST_CONNECTION_STRING")]
    public async Task PostgreSqlAllowsOnlyOneConcurrentProductWithTheSameNormalizedWebsiteUrl()
    {
        var options = new DbContextOptionsBuilder<GuidoraDbContext>()
            .UseNpgsql(Environment.GetEnvironmentVariable("GUIDORA_TEST_CONNECTION_STRING"))
            .Options;
        var suffix = Guid.NewGuid().ToString("N");
        var websiteUrl = $"https://duplicate-{suffix}.example.test/product";
        int userId;

        await using (var setup = new GuidoraDbContext(options))
        {
            var user = new User
            {
                FirstName = "Unique",
                LastName = "Index",
                Email = $"unique-index-{suffix}@example.test",
                PasswordHash = [1],
                PasswordSalt = [1],
                Status = true
            };
            setup.Users.Add(user);
            await setup.SaveChangesAsync();
            userId = user.Id;
        }

        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var readyCount = 0;

        async Task<Exception?> InsertProductAsync(string name, string slug)
        {
            await using var context = new GuidoraDbContext(options);
            context.Products.Add(new Product
            {
                Name = name,
                Slug = slug,
                ShortDescription = "Unique URL test",
                Description = "Concurrent uniqueness regression test.",
                WebsiteUrl = websiteUrl,
                LogoUrl = "https://example.test/logo.png",
                CreatedByUserId = userId,
                ProductStatusId = 1,
                CreatedAt = DateTime.UtcNow
            });

            if (Interlocked.Increment(ref readyCount) == 2) ready.SetResult();
            await ready.Task;

            try
            {
                await new GuidoraUnitOfWork(context).SaveChangesAsync();
                return null;
            }
            catch (DataConflictException exception)
            {
                return exception;
            }
        }

        try
        {
            var outcomes = await Task.WhenAll(
                InsertProductAsync($"Concurrent A {suffix}", $"concurrent-a-{suffix}"),
                InsertProductAsync($"Concurrent B {suffix}", $"concurrent-b-{suffix}"));

            Assert.Single(outcomes, outcome => outcome is null);
            Assert.IsType<DataConflictException>(Assert.Single(outcomes, outcome => outcome is not null));
            await using var verification = new GuidoraDbContext(options);
            Assert.Equal(1, await verification.Products.CountAsync(product => product.WebsiteUrl == websiteUrl));
        }
        finally
        {
            await using var cleanup = new GuidoraDbContext(options);
            var product = await cleanup.Products.SingleOrDefaultAsync(item => item.WebsiteUrl == websiteUrl);
            if (product is not null) cleanup.Products.Remove(product);
            var user = await cleanup.Users.SingleOrDefaultAsync(item => item.Id == userId);
            if (user is not null) cleanup.Users.Remove(user);
            await cleanup.SaveChangesAsync();
        }
    }

    [RequiresEnvironmentFact("GUIDORA_TEST_CONNECTION_STRING")]
    public async Task RefreshTokenRotationAllowsOnlyOneConcurrentConsumer()
    {
        var options = new DbContextOptionsBuilder<GuidoraDbContext>()
            .UseNpgsql(Environment.GetEnvironmentVariable("GUIDORA_TEST_CONNECTION_STRING")).Options;
        var sessionId = Guid.NewGuid();
        var firstReplacementId = Guid.NewGuid();
        var secondReplacementId = Guid.NewGuid();
        int userId;

        await using (var setup = new GuidoraDbContext(options))
        {
            var user = new User
            {
                FirstName = "Concurrent", LastName = "Refresh", Email = $"refresh-{Guid.NewGuid():N}@example.test",
                PasswordHash = [1], PasswordSalt = [1], Status = true
            };
            setup.Users.Add(user);
            await setup.SaveChangesAsync();
            userId = user.Id;
            setup.UserSessions.Add(new UserSession
            {
                Id = sessionId, UserId = userId, RefreshTokenHash = Guid.NewGuid().ToString("N"),
                CreatedAt = DateTime.UtcNow, ExpiresAt = DateTime.UtcNow.AddDays(1), DeviceName = "test"
            });
            await setup.SaveChangesAsync();
        }

        try
        {
            var revokedAt = DateTime.UtcNow;
            async Task<bool> TryRotateAsync(Guid replacementId)
            {
                await using var context = new GuidoraDbContext(options);
                return await new EfUserSessionDal(context).TryRotateAsync(sessionId, replacementId, revokedAt);
            }

            var outcomes = await Task.WhenAll(TryRotateAsync(firstReplacementId), TryRotateAsync(secondReplacementId));
            Assert.Single(outcomes, success => success);
            Assert.Single(outcomes, success => !success);

            await using var verify = new GuidoraDbContext(options);
            var rotated = await verify.UserSessions.SingleAsync(item => item.Id == sessionId);
            Assert.InRange(Math.Abs((rotated.RevokedAt!.Value - revokedAt).Ticks), 0, 10);
            Assert.True(rotated.ReplacedBySessionId == firstReplacementId ||
                        rotated.ReplacedBySessionId == secondReplacementId);
        }
        finally
        {
            await using var cleanup = new GuidoraDbContext(options);
            var session = await cleanup.UserSessions.SingleOrDefaultAsync(item => item.Id == sessionId);
            if (session is not null) cleanup.UserSessions.Remove(session);
            var user = await cleanup.Users.SingleOrDefaultAsync(item => item.Id == userId);
            if (user is not null) cleanup.Users.Remove(user);
            await cleanup.SaveChangesAsync();
        }
    }

    [RequiresEnvironmentFact("GUIDORA_TEST_CONNECTION_STRING")]
    public async Task ConcurrentLogoutRevokesAndConsumesAnActiveSessionOnlyOnce()
    {
        var options = new DbContextOptionsBuilder<GuidoraDbContext>()
            .UseNpgsql(Environment.GetEnvironmentVariable("GUIDORA_TEST_CONNECTION_STRING")).Options;
        var sessionId = Guid.NewGuid();
        int userId;
        await using (var setup = new GuidoraDbContext(options))
        {
            var user = new User
            {
                FirstName = "Concurrent", LastName = "Logout", Email = $"logout-{Guid.NewGuid():N}@example.test",
                PasswordHash = [1], PasswordSalt = [1], Status = true
            };
            setup.Users.Add(user);
            await setup.SaveChangesAsync();
            userId = user.Id;
            setup.UserSessions.Add(new UserSession
            {
                Id = sessionId, UserId = userId, RefreshTokenHash = Guid.NewGuid().ToString("N"),
                CreatedAt = DateTime.UtcNow, ExpiresAt = DateTime.UtcNow.AddDays(1), DeviceName = "test"
            });
            await setup.SaveChangesAsync();
        }

        try
        {
            var revokedAt = DateTime.UtcNow;
            async Task<bool> TryRevokeAsync()
            {
                await using var context = new GuidoraDbContext(options);
                return await new EfUserSessionDal(context).TryRevokeAsync(sessionId, revokedAt);
            }

            var outcomes = await Task.WhenAll(TryRevokeAsync(), TryRevokeAsync());
            Assert.Single(outcomes, success => success);
            Assert.Single(outcomes, success => !success);

            await using var verify = new GuidoraDbContext(options);
            Assert.NotNull((await verify.UserSessions.SingleAsync(item => item.Id == sessionId)).RevokedAt);
        }
        finally
        {
            await using var cleanup = new GuidoraDbContext(options);
            var session = await cleanup.UserSessions.SingleOrDefaultAsync(item => item.Id == sessionId);
            if (session is not null) cleanup.UserSessions.Remove(session);
            var user = await cleanup.Users.SingleOrDefaultAsync(item => item.Id == userId);
            if (user is not null) cleanup.Users.Remove(user);
            await cleanup.SaveChangesAsync();
        }
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
    public async Task ReportReviewUpdatesRejectAStalePostgreSqlXminVersion()
    {
        var options = new DbContextOptionsBuilder<GuidoraDbContext>()
            .UseNpgsql(Environment.GetEnvironmentVariable("GUIDORA_TEST_CONNECTION_STRING")).Options;
        var suffix = Guid.NewGuid().ToString("N");
        int reportId;
        int productId;
        int userId;

        await using (var setup = new GuidoraDbContext(options))
        {
            var user = new User
            {
                FirstName = "Concurrency", LastName = "Report", Email = $"report-{suffix}@example.test",
                PasswordHash = [1], PasswordSalt = [1], Status = true
            };
            setup.Users.Add(user);
            await setup.SaveChangesAsync();
            userId = user.Id;
            var published = await setup.ProductStatuses.SingleAsync(item => item.Code == "published");
            var product = new Product
            {
                Name = $"Report-{suffix}", Slug = $"report-{suffix}", ShortDescription = "Concurrency test",
                Description = "Test product", WebsiteUrl = $"https://example.test/{suffix}",
                LogoUrl = "https://example.test/logo.png", ProductStatusId = published.Id,
                CreatedByUserId = userId, CreatedAt = DateTime.UtcNow
            };
            setup.Products.Add(product);
            await setup.SaveChangesAsync();
            productId = product.ProductId;
            var report = new ProductReport
            {
                ProductId = productId, ReportedByUserId = userId, ReportReasonId = 1,
                ReportStatusId = 1, Description = "initial", CreatedAt = DateTime.UtcNow
            };
            setup.ProductReports.Add(report);
            await setup.SaveChangesAsync();
            reportId = report.Id;
        }

        try
        {
            await using var first = new GuidoraDbContext(options);
            await using var second = new GuidoraDbContext(options);
            var firstReport = await first.ProductReports.SingleAsync(item => item.Id == reportId);
            var staleReport = await second.ProductReports.SingleAsync(item => item.Id == reportId);
            firstReport.Description = "resolved by writer one";
            firstReport.ReportStatusId = 2;
            firstReport.ResolvedByUserId = userId;
            firstReport.ResolvedAt = DateTime.UtcNow;
            await first.SaveChangesAsync();

            staleReport.Description = "stale writer";
            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
        }
        finally
        {
            await using var cleanup = new GuidoraDbContext(options);
            var product = await cleanup.Products.SingleOrDefaultAsync(item => item.ProductId == productId);
            if (product is not null) cleanup.Products.Remove(product);
            var user = await cleanup.Users.SingleOrDefaultAsync(item => item.Id == userId);
            if (user is not null) cleanup.Users.Remove(user);
            await cleanup.SaveChangesAsync();
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
        var productCacheDirective = Assert.IsAssignableFrom<ICacheInvalidationDirective>(productResult);
        Assert.Contains(productCacheDirective.CacheInvalidations, target =>
            target.Profile == CacheProfiles.ProductDetail && target.ArgumentIndex == 0 &&
            Equals(target.ArgumentValue, product.ProductId));
        var categoryCacheDirective = Assert.IsAssignableFrom<ICacheInvalidationDirective>(categoryResult);
        Assert.Contains(categoryCacheDirective.CacheInvalidations,
            target => target.Profile == CacheProfiles.CatalogNavigation);
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
        Assert.Contains(policy, pair => pair.Value == "volatile-lfu");
        var persistence = await server.ConfigGetAsync("appendonly");
        Assert.Contains(persistence, pair => pair.Value == "no");
    }

    [RequiresEnvironmentFact("GUIDORA_TEST_REDIS_CONFIGURATION")]
    public async Task RedisPatternInvalidationDeletesKeysInBoundedBatches()
    {
        var options = ConfigurationOptions.Parse(Environment.GetEnvironmentVariable("GUIDORA_TEST_REDIS_CONFIGURATION")!);
        options.AbortOnConnectFail = false;
        await using var connection = await ConnectionMultiplexer.ConnectAsync(options);
        var cache = new RedisCacheManager(connection);
        var profile = $"integration-batch-{Guid.NewGuid():N}";
        var keys = Enumerable.Range(0, 600)
            .Select(index => $"{profile}:entry:{index}")
            .ToArray();

        foreach (var key in keys)
            await cache.AddAsync(key, "value", durationMinutes: 5);

        var hit = await cache.TryGetAsync(keys[0]);
        Assert.True(hit.Found);
        Assert.Equal("value", hit.Value);

        Assert.Equal(0, await cache.GetProfileGenerationAsync(profile));
        await cache.AdvanceProfileGenerationAsync(profile);
        Assert.Equal(1, await cache.GetProfileGenerationAsync(profile));

        await cache.RemoveByPatternAsync($"{profile}:*");

        Assert.All(keys, key => Assert.False(cache.IsExists(key)));
    }

    [RequiresEnvironmentFact("GUIDORA_TEST_CONNECTION_STRING")]
    public async Task BotVerificationSeedMigrationPreservesReferencedHistoricalLookup()
    {
        var options = new DbContextOptionsBuilder<GuidoraDbContext>()
            .UseNpgsql(Environment.GetEnvironmentVariable("GUIDORA_TEST_CONNECTION_STRING"))
            .Options;
        await using var context = new GuidoraDbContext(options);
        await using var transaction = await context.Database.BeginTransactionAsync();
        await context.Database.ExecuteSqlRawAsync("""
            CREATE TEMP TABLE "VerificationTypes" ("Id" integer PRIMARY KEY, "Code" text, "Name" text);
            CREATE TEMP TABLE "ProductVerifications" ("VerificationTypeId" integer NOT NULL);
            INSERT INTO "VerificationTypes" VALUES (3, 'bot', 'Bot verified');
            INSERT INTO "ProductVerifications" VALUES (3);
            """);

        var migration = new global::DataAccess.Migrations.RemoveFutureBotVerificationSeed();
        var migrationBuilder = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");
        typeof(Migration).GetMethod("Up", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(migration, [migrationBuilder]);
        var cleanupSql = Assert.IsType<SqlOperation>(Assert.Single(migrationBuilder.Operations)).Sql!;

        await context.Database.ExecuteSqlRawAsync(cleanupSql);
        var remainsWhileReferenced = await context.Database.SqlQueryRaw<bool>(
            "SELECT EXISTS (SELECT 1 FROM \"VerificationTypes\" WHERE \"Code\" = 'bot') AS \"Value\"")
            .SingleAsync();
        Assert.True(remainsWhileReferenced);

        await context.Database.ExecuteSqlRawAsync("DELETE FROM \"ProductVerifications\"");
        await context.Database.ExecuteSqlRawAsync(cleanupSql);
        var remainsAfterReferenceRemoval = await context.Database.SqlQueryRaw<bool>(
            "SELECT EXISTS (SELECT 1 FROM \"VerificationTypes\" WHERE \"Code\" = 'bot') AS \"Value\"")
            .SingleAsync();
        Assert.False(remainsAfterReferenceRemoval);
        await transaction.RollbackAsync();
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
