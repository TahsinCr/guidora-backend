using Business.Abstract;
using Business.Concrete;
using Core.Entities.Concrete;
using Core.Pagination;
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
    public async Task LoginFailuresUseOnePublicCredentialResult()
    {
        HashingHelper.CreatePasswordHash("correct password", out var hash, out var salt);
        var account = new User { Id = 17, Email = "known@example.test", Status = true,
            PasswordHash = hash, PasswordSalt = salt };
        var users = new UserServiceStub(account);
        var manager = CreateManager(users, new UserSessionDalStub(),
            new AuthenticationStateDalStub(), new RecordingDistributedCache());

        var missing = await manager.LoginAsync(new UserForLoginDto
            { Email = "missing@example.test", Password = "wrong password" });
        var wrong = await manager.LoginAsync(new UserForLoginDto
            { Email = account.Email, Password = "wrong password" });
        account.Status = false;
        var inactive = await manager.LoginAsync(new UserForLoginDto
            { Email = account.Email, Password = "correct password" });

        Assert.False(missing.Success);
        Assert.Equal(missing.Message, wrong.Message);
        Assert.Equal(missing.Message, inactive.Message);
    }
    [Fact]
    public async Task SessionManagementListsOnlyOwnedActiveSessionsAndRevokesOnlyOwnedTargets()
    {
        var current = new UserSession { Id = Guid.NewGuid(), UserId = 17, DeviceName = "current",
            CreatedAt = DateTime.UtcNow, ExpiresAt = DateTime.UtcNow.AddDays(1),
            AbsoluteExpiresAt = DateTime.UtcNow.AddDays(2) };
        var other = new UserSession { Id = Guid.NewGuid(), UserId = 17, DeviceName = "other",
            CreatedAt = DateTime.UtcNow.AddHours(-1), ExpiresAt = DateTime.UtcNow.AddDays(1),
            AbsoluteExpiresAt = DateTime.UtcNow.AddDays(2) };
        var foreign = new UserSession { Id = Guid.NewGuid(), UserId = 18, DeviceName = "foreign",
            CreatedAt = DateTime.UtcNow, ExpiresAt = DateTime.UtcNow.AddDays(1),
            AbsoluteExpiresAt = DateTime.UtcNow.AddDays(2) };
        var sessions = new UserSessionDalStub([current, other, foreign]);
        var cache = new RecordingDistributedCache();
        var manager = CreateManager(new UserServiceStub(new User { Id = 17, Status = true }),
            sessions, new AuthenticationStateDalStub(), cache);

        var page = await manager.GetSessionsAsync(17, current.Id, 1, 20);
        Assert.Equal(2, page.Data.TotalCount);
        Assert.Single(page.Data.Items, item => item.IsCurrent);
        Assert.DoesNotContain(page.Data.Items, item => item.Id == foreign.Id);
        Assert.True((await manager.RevokeOwnedSessionAsync(17, foreign.Id)).Success);
        Assert.Null(foreign.RevokedAt);
        Assert.True((await manager.RevokeOtherSessionsAsync(17, current.Id)).Success);
        Assert.Null(current.RevokedAt);
        Assert.NotNull(other.RevokedAt);
        Assert.Contains($"auth-session:{other.Id}", cache.RemovedKeys);
    }

    [Fact]
    public async Task LogoutOthersContinuesCacheEvictionAfterOneFailure()
    {
        var current = new UserSession { Id = Guid.NewGuid(), UserId = 17,
            ExpiresAt = DateTime.UtcNow.AddDays(1), AbsoluteExpiresAt = DateTime.UtcNow.AddDays(2) };
        var first = new UserSession { Id = Guid.NewGuid(), UserId = 17,
            ExpiresAt = DateTime.UtcNow.AddDays(1), AbsoluteExpiresAt = DateTime.UtcNow.AddDays(2) };
        var second = new UserSession { Id = Guid.NewGuid(), UserId = 17,
            ExpiresAt = DateTime.UtcNow.AddDays(1), AbsoluteExpiresAt = DateTime.UtcNow.AddDays(2) };
        var cache = new RecordingDistributedCache { FailFirstRemoval = true };
        var manager = CreateManager(new UserServiceStub(new User { Id = 17, Status = true }),
            new UserSessionDalStub([current, first, second]), new AuthenticationStateDalStub(), cache);

        Assert.True((await manager.RevokeOtherSessionsAsync(17, current.Id)).Success);
        Assert.Equal(2, cache.RemovedKeys.Count);
        Assert.NotNull(first.RevokedAt);
        Assert.NotNull(second.RevokedAt);
    }

    [Fact]
    public async Task RevokingAnOldSessionIdClosesItsCurrentRotatedDescendant()
    {
        var familyId = Guid.NewGuid();
        var old = new UserSession { Id = familyId, SessionFamilyId = familyId,
            UserId = 17, RevokedAt = DateTime.UtcNow.AddMinutes(-1) };
        var current = new UserSession { Id = Guid.NewGuid(), SessionFamilyId = familyId,
            UserId = 17, ExpiresAt = DateTime.UtcNow.AddDays(1),
            AbsoluteExpiresAt = DateTime.UtcNow.AddDays(2) };
        var cache = new RecordingDistributedCache();
        var manager = CreateManager(new UserServiceStub(new User { Id = 17, Status = true }),
            new UserSessionDalStub([old, current]), new AuthenticationStateDalStub(), cache);

        Assert.True((await manager.RevokeOwnedSessionAsync(17, old.Id)).Success);
        Assert.NotNull(current.RevokedAt);
        Assert.Contains($"auth-session:{current.Id}", cache.RemovedKeys);
    }
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
        oldSession.SessionFamilyId = oldSession.Id;
        var sessions = new UserSessionDalStub([oldSession]);
        var cache = new RecordingDistributedCache();
        var manager = CreateManager(new UserServiceStub(user), sessions,
            new AuthenticationStateDalStub([new UserAuthenticationState
                { UserId = user.Id, AuthorizationVersion = 4 }]), cache);

        var result = await manager.RefreshSessionAsync(rawRefreshToken, "test client");

        Assert.True(result.Success);
        Assert.NotEqual(rawRefreshToken, result.Data.RefreshToken);
        Assert.NotNull(oldSession.RevokedAt);
        Assert.NotNull(oldSession.LastUsedAt);
        Assert.Equal(result.Data.AccessToken.SessionId, oldSession.ReplacedBySessionId);
        Assert.Contains(sessions.Records, session => session.Id == result.Data.AccessToken.SessionId);
        Assert.Equal(oldSession.SessionFamilyId, sessions.Records.Single(item =>
            item.Id == result.Data.AccessToken.SessionId).SessionFamilyId);
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
    public async Task LogoutWithRotatedRefreshTokenRevokesCurrentFamilyDescendant()
    {
        const string staleRefreshToken = "stale-logout-refresh-secret";
        var familyId = Guid.NewGuid();
        var oldSession = new UserSession
        {
            Id = familyId,
            SessionFamilyId = familyId,
            UserId = 17,
            RefreshTokenHash = RefreshTokenHasher.Hash(staleRefreshToken),
            RevokedAt = DateTime.UtcNow.AddMinutes(-1),
            ReplacedBySessionId = Guid.NewGuid()
        };
        var currentSession = new UserSession
        {
            Id = oldSession.ReplacedBySessionId!.Value,
            SessionFamilyId = familyId,
            UserId = 17,
            ExpiresAt = DateTime.UtcNow.AddDays(1),
            AbsoluteExpiresAt = DateTime.UtcNow.AddDays(2)
        };
        var cache = new RecordingDistributedCache();
        var manager = CreateManager(new UserServiceStub(new User { Id = 17, Status = true }),
            new UserSessionDalStub([oldSession, currentSession]), new AuthenticationStateDalStub(), cache);

        var result = await manager.RevokeSessionAsync(staleRefreshToken);

        Assert.True(result.Success);
        Assert.NotNull(currentSession.RevokedAt);
        Assert.Contains($"auth-session:{currentSession.Id}", cache.RemovedKeys);
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
    public async Task ExpiredAbsoluteRevokedRefreshDoesNotInvalidateCurrentSessions()
    {
        const string rawRefreshToken = "expired-replayed-refresh-secret";
        var session = new UserSession
        {
            Id = Guid.NewGuid(), UserId = 23,
            RefreshTokenHash = RefreshTokenHasher.Hash(rawRefreshToken),
            CreatedAt = DateTime.UtcNow.AddDays(-200),
            ExpiresAt = DateTime.UtcNow.AddDays(-170),
            AbsoluteExpiresAt = DateTime.UtcNow.AddDays(-20),
            RevokedAt = DateTime.UtcNow.AddDays(-190),
            ReplacedBySessionId = Guid.NewGuid()
        };
        var invalidation = new SessionInvalidationStub();
        var manager = CreateManager(new UserServiceStub(new User { Id = 23, Status = true }),
            new UserSessionDalStub([session]), new AuthenticationStateDalStub(),
            new RecordingDistributedCache(), invalidation);

        Assert.False((await manager.RefreshSessionAsync(rawRefreshToken, "test client")).Success);
        Assert.Empty(invalidation.InvalidatedUsers);
        Assert.Null(session.ReplayDetectedAt);
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
            sessions, configuration, cache, invalidation ?? new SessionInvalidationStub(), new UnitOfWorkStub(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<AuthManager>.Instance);
    }

    private sealed class UserServiceStub(User user) : IUserService
    {
        public Task<IDataResult<List<OperationClaim>>> GetClaimsAsync(User value, CancellationToken cancellationToken = default) =>
            Task.FromResult<IDataResult<List<OperationClaim>>>(new SuccessDataResult<List<OperationClaim>>([]));

        public Task<IDataResult<User>> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
            Task.FromResult<IDataResult<User>>(id == user.Id ? new SuccessDataResult<User>(user) : new ErrorDataResult<User>("missing"));
        public Task<IDataResult<User>> GetByMailAsync(string email, CancellationToken cancellationToken = default) =>
            Task.FromResult<IDataResult<User>>(email == user.Email
                ? new SuccessDataResult<User>(user)
                : new ErrorDataResult<User>("missing"));
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
        public Task<PagedResult<UserSessionSummaryDto>> GetActivePageAsync(int userId, Guid currentSessionId,
            int page, int pageSize, DateTime now, CancellationToken cancellationToken = default)
        {
            var active = GetAll().Where(item => item.UserId == userId && item.RevokedAt == null &&
                item.ExpiresAt > now && item.AbsoluteExpiresAt > now).OrderByDescending(item => item.CreatedAt).ToList();
            var items = active.Skip((page - 1) * pageSize).Take(pageSize).Select(item =>
                new UserSessionSummaryDto(item.Id, item.DeviceName, item.CreatedAt, item.LastUsedAt,
                    item.ExpiresAt, item.AbsoluteExpiresAt, item.IsPersistent, item.Id == currentSessionId)).ToList();
            return Task.FromResult(new PagedResult<UserSessionSummaryDto>(items, page, pageSize, active.Count));
        }
        public Task<IReadOnlyList<Guid>> RevokeOwnedFamilyAsync(int userId, Guid sessionId, DateTime now,
            CancellationToken cancellationToken = default)
        {
            var item = Get(value => value.UserId == userId && value.Id == sessionId);
            if (item == null) return Task.FromResult<IReadOnlyList<Guid>>([]);
            var familyId = item.SessionFamilyId == Guid.Empty ? item.Id : item.SessionFamilyId;
            var items = GetAll().Where(value => value.UserId == userId && value.RevokedAt == null &&
                (value.SessionFamilyId == Guid.Empty ? value.Id : value.SessionFamilyId) == familyId).ToList();
            foreach (var value in items) value.RevokedAt = now;
            return Task.FromResult<IReadOnlyList<Guid>>(items.Select(value => value.Id).ToList());
        }
        public Task<IReadOnlyList<Guid>> RevokeOtherActiveAsync(int userId, Guid currentSessionId, DateTime now,
            CancellationToken cancellationToken = default)
        {
            var items = GetAll().Where(item => item.UserId == userId && item.Id != currentSessionId &&
                item.RevokedAt == null && item.ExpiresAt > now && item.AbsoluteExpiresAt > now).ToList();
            foreach (var item in items) item.RevokedAt = now;
            return Task.FromResult<IReadOnlyList<Guid>>(items.Select(item => item.Id).ToList());
        }
        public Task<bool> TryRotateAsync(Guid sessionId, Guid replacementSessionId, DateTime revokedAt,
            CancellationToken cancellationToken = default)
        {
            var session = Get(item => item.Id == sessionId && item.RevokedAt == null && item.ExpiresAt > revokedAt);
            if (session is null) return Task.FromResult(false);
            session.RevokedAt = revokedAt;
            session.LastUsedAt = revokedAt;
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
        public AccessToken CreateToken(TokenSubject subject, IReadOnlyCollection<string> operationClaims,
            Guid sessionId,
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
        public bool FailFirstRemoval { get; init; }
        public byte[]? Get(string key) => null;
        public Task<byte[]?> GetAsync(string key, CancellationToken token = default) => Task.FromResult<byte[]?>(null);
        public void Refresh(string key) { }
        public Task RefreshAsync(string key, CancellationToken token = default) => Task.CompletedTask;
        public void Remove(string key)
        {
            RemovedKeys.Add(key);
            if (FailFirstRemoval && RemovedKeys.Count == 1)
                throw new InvalidOperationException("Redis unavailable");
        }
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
