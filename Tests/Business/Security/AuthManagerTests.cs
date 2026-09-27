using Business.Abstract;
using Business.Concrete;
using Core.Entities.Concrete;
using Core.Utilities.Results;
using Core.Utilities.Security.Hashing;
using Core.Utilities.Security.JWT;
using DataAccess.Abstract;
using Entities;
using Entities.DTOs;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Tests.Core.TestDoubles;

namespace Tests.Business.Security;

public sealed class AuthManagerTests
{
    [Fact]
    public async Task RefreshRotatesTokenAndRevokesOldSessionOnlyAfterSuccessfulAtomicUpdate()
    {
        const string rawRefreshToken = "refresh-secret";
        var user = new User { Id = 17, Status = true };
        var oldSession = new UserSession
        {
            Id = Guid.NewGuid(), UserId = user.Id,
            RefreshTokenHash = RefreshTokenHasher.Hash(rawRefreshToken),
            CreatedAt = DateTime.UtcNow.AddDays(-1), ExpiresAt = DateTime.UtcNow.AddDays(10),
            AbsoluteExpiresAt = DateTime.UtcNow.AddDays(179)
        };
        var sessions = new UserSessionDalStub([oldSession]);
        var cache = new RecordingDistributedCache();
        var manager = CreateManager(new UserServiceStub(user), sessions,
            new AuthenticationStateDalStub([new UserAuthenticationState
                { UserId = user.Id, AuthorizationVersion = 4 }]), cache);

        var result = await manager.RefreshSessionAsync(rawRefreshToken, "test client");

        Assert.True(result.Success);
        Assert.NotEqual(rawRefreshToken, result.Data.RefreshToken);
        Assert.NotNull(oldSession.RevokedAt);
        Assert.Equal(result.Data.AccessToken.SessionId, oldSession.ReplacedBySessionId);
        Assert.Contains(sessions.Records, session => session.Id == result.Data.AccessToken.SessionId);
        Assert.Contains($"auth-session:{oldSession.Id}", cache.RemovedKeys);
    }

    [Fact]
    public async Task LogoutRevokesSessionAtomicallyAndRemovesItsValidationCache()
    {
        const string rawRefreshToken = "logout-refresh-secret";
        var session = new UserSession
        {
            Id = Guid.NewGuid(), UserId = 17,
            RefreshTokenHash = RefreshTokenHasher.Hash(rawRefreshToken),
            CreatedAt = DateTime.UtcNow.AddDays(-1), ExpiresAt = DateTime.UtcNow.AddDays(10),
            AbsoluteExpiresAt = DateTime.UtcNow.AddDays(179)
        };
        var cache = new RecordingDistributedCache();
        var manager = CreateManager(new UserServiceStub(new User { Id = 17, Status = true }),
            new UserSessionDalStub([session]), new AuthenticationStateDalStub(), cache);

        Assert.True((await manager.RevokeSessionAsync(rawRefreshToken)).Success);
        Assert.NotNull(session.RevokedAt);
        Assert.Equal([$"auth-session:{session.Id}"], cache.RemovedKeys);
    }

    [Fact]
    public async Task ReplayedRefreshInvalidatesAllUserSessionsOutsideTheFailedRotation()
    {
        const string rawRefreshToken = "replayed-refresh-secret";
        var session = new UserSession
        {
            Id = Guid.NewGuid(), UserId = 23,
            RefreshTokenHash = RefreshTokenHasher.Hash(rawRefreshToken),
            CreatedAt = DateTime.UtcNow.AddDays(-1), ExpiresAt = DateTime.UtcNow.AddDays(10),
            AbsoluteExpiresAt = DateTime.UtcNow.AddDays(179), RevokedAt = DateTime.UtcNow.AddMinutes(-1),
            ReplacedBySessionId = Guid.NewGuid()
        };
        var invalidation = new SessionInvalidationStub();
        var manager = CreateManager(new UserServiceStub(new User { Id = 23, Status = true }),
            new UserSessionDalStub([session]), new AuthenticationStateDalStub(),
            new RecordingDistributedCache(), invalidation);

        var result = await manager.RefreshSessionAsync(rawRefreshToken, "test client");

        Assert.False(result.Success);
        Assert.Equal([23], invalidation.InvalidatedUsers);
        Assert.NotNull(session.ReplayDetectedAt);

        await manager.RefreshSessionAsync(rawRefreshToken, "test client");
        Assert.Equal([23], invalidation.InvalidatedUsers);
    }

    [Fact]
    public async Task FailedConcurrentRotationHandlesReplayOnlyOnce()
    {
        const string rawRefreshToken = "racing-refresh-secret";
        var session = new UserSession
        {
            Id = Guid.NewGuid(), UserId = 31,
            RefreshTokenHash = RefreshTokenHasher.Hash(rawRefreshToken),
            CreatedAt = DateTime.UtcNow.AddDays(-1), ExpiresAt = DateTime.UtcNow.AddDays(10),
            AbsoluteExpiresAt = DateTime.UtcNow.AddDays(179)
        };
        var sessions = new UserSessionDalStub([session]) { ForceRotationConflict = true };
        var invalidation = new SessionInvalidationStub();
        var manager = CreateManager(new UserServiceStub(new User { Id = 31, Status = true }),
            sessions, new AuthenticationStateDalStub(), new RecordingDistributedCache(), invalidation);

        Assert.False((await manager.RefreshSessionAsync(rawRefreshToken, "test client")).Success);
        Assert.NotNull(session.ReplayDetectedAt);
        await manager.RefreshSessionAsync(rawRefreshToken, "test client");
        Assert.Equal([31], invalidation.InvalidatedUsers);
    }

    private static AuthManager CreateManager(IUserService users, UserSessionDalStub sessions,
        IUserAuthenticationStateDal states, RecordingDistributedCache cache,
        IUserSessionInvalidationService? invalidation = null)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["TokenOptions:Issuer"] = "guidora-tests",
            ["TokenOptions:Audience"] = "guidora-tests",
            ["TokenOptions:RefreshTokenExpirationDays"] = "30",
            ["TokenOptions:RememberMeExpirationDays"] = "90",
            ["TokenOptions:SessionAbsoluteExpirationDays"] = "180"
        }).Build();

        return new AuthManager(users, new RoleDalStub(), new UserRoleDalStub(), new TokenHelperStub(), states,
            sessions, configuration, cache, invalidation ?? new SessionInvalidationStub(), new UnitOfWorkStub());
    }

    private sealed class UserServiceStub(User user) : IUserService
    {
        public Task<IDataResult<List<OperationClaim>>> GetClaimsAsync(User value, CancellationToken cancellationToken = default) =>
            Task.FromResult<IDataResult<List<OperationClaim>>>(new SuccessDataResult<List<OperationClaim>>([]));
        public Task<IResult> GrantOperationClaimAsync(User value, OperationClaim operationClaim, CancellationToken cancellationToken = default) =>
            Task.FromResult<IResult>(new SuccessResult());
        public Task<IDataResult<User>> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
            Task.FromResult<IDataResult<User>>(id == user.Id ? new SuccessDataResult<User>(user) : new ErrorDataResult<User>("missing"));
        public Task<IDataResult<User>> GetByMailAsync(string email, CancellationToken cancellationToken = default) =>
            Task.FromResult<IDataResult<User>>(new ErrorDataResult<User>("missing"));
        public Task<IResult> RegisterUserAsync(User value, CancellationToken cancellationToken = default) =>
            Task.FromResult<IResult>(new SuccessResult());
        public Task<IResult> UpdateUserAsync(User value, CancellationToken cancellationToken = default) =>
            Task.FromResult<IResult>(new SuccessResult());
    }

    private sealed class UserSessionDalStub(IEnumerable<UserSession>? values = null)
        : EntityRepositoryStub<UserSession>(values), IUserSessionDal
    {
        public IReadOnlyList<UserSession> Records => GetAll();
        public bool ForceRotationConflict { get; init; }
        public Task<bool> TryRotateAsync(Guid sessionId, Guid replacementSessionId, DateTime revokedAt,
            CancellationToken cancellationToken = default)
        {
            var session = Get(item => item.Id == sessionId && item.RevokedAt == null && item.ExpiresAt > revokedAt);
            if (session is null) return Task.FromResult(false);
            session.RevokedAt = revokedAt;
            session.ReplacedBySessionId = replacementSessionId;
            return Task.FromResult(!ForceRotationConflict);
        }
        public Task<bool> TryRevokeAsync(Guid sessionId, DateTime revokedAt, CancellationToken cancellationToken = default)
        {
            var session = Get(item => item.Id == sessionId && item.RevokedAt == null);
            if (session is null) return Task.FromResult(false);
            session.RevokedAt = revokedAt;
            return Task.FromResult(true);
        }
        public Task<bool> TryMarkReplayDetectedAsync(Guid sessionId, DateTime detectedAt,
            CancellationToken cancellationToken = default)
        {
            var session = Get(item => item.Id == sessionId && item.ReplacedBySessionId != null &&
                                      item.ReplayDetectedAt == null);
            if (session is null) return Task.FromResult(false);
            session.ReplayDetectedAt = detectedAt;
            return Task.FromResult(true);
        }
    }

    private sealed class AuthenticationStateDalStub(IEnumerable<UserAuthenticationState>? values = null)
        : EntityRepositoryStub<UserAuthenticationState>(values), IUserAuthenticationStateDal
    {
        public Task IncrementAuthorizationVersionsAsync(IReadOnlyCollection<int> userIds, DateTime updatedAt,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
    private sealed class RoleDalStub : EntityRepositoryStub<Role>, IRoleDal
    {
        public RoleDalStub() : base([]) { }
    }
    private sealed class UserRoleDalStub : EntityRepositoryStub<UserRole>, IUserRoleDal
    {
        public UserRoleDalStub() : base([]) { }
        public override void Purge(UserRole entity) => base.Purge(entity);
        public override void PurgeRange(IEnumerable<UserRole> entities) => base.PurgeRange(entities);
    }

    private sealed class TokenHelperStub : ITokenHelper
    {
        public AccessToken CreateToken(User user, List<OperationClaim> operationClaims, Guid sessionId,
            int authorizationVersion) => new() { Token = "access-token", SessionId = sessionId,
            Expiration = DateTime.UtcNow.AddMinutes(15) };
    }

    private sealed class SessionInvalidationStub : IUserSessionInvalidationService
    {
        public List<int> InvalidatedUsers { get; } = [];
        public Task<IResult> InvalidateUserSessionsAsync(int userId, CancellationToken cancellationToken = default)
        {
            InvalidatedUsers.Add(userId);
            return Task.FromResult<IResult>(new SuccessResult());
        }
        public Task<IResult> InvalidateUsersSessionsAsync(IReadOnlyCollection<int> userIds,
            CancellationToken cancellationToken = default)
        {
            InvalidatedUsers.AddRange(userIds);
            return Task.FromResult<IResult>(new SuccessResult());
        }
    }

    private sealed class RecordingDistributedCache : IDistributedCache
    {
        public List<string> RemovedKeys { get; } = [];
        public byte[]? Get(string key) => null;
        public Task<byte[]?> GetAsync(string key, CancellationToken token = default) => Task.FromResult<byte[]?>(null);
        public void Refresh(string key) { }
        public Task RefreshAsync(string key, CancellationToken token = default) => Task.CompletedTask;
        public void Remove(string key) => RemovedKeys.Add(key);
        public Task RemoveAsync(string key, CancellationToken token = default)
        {
            Remove(key);
            return Task.CompletedTask;
        }
        public void Set(string key, byte[] value, DistributedCacheEntryOptions options) { }
        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options,
            CancellationToken token = default) => Task.CompletedTask;
    }
}
