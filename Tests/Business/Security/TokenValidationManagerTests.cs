using System.Text;
using Business.Concrete;
using Core.Entities.Concrete;
using Core.Utilities.Results;
using DataAccess.Abstract;
using Entities;
using Microsoft.Extensions.Caching.Distributed;
using Tests.Core.TestDoubles;

namespace Tests.Business.Security;

public sealed class TokenValidationManagerTests
{
    [Fact]
    public async Task ValidatesSessionWithAsyncDalAndCachesAuthorizationVersion()
    {
        var sessionId = Guid.NewGuid();
        var cache = new InMemoryDistributedCacheStub();
        var manager = new TokenValidationManager(
            new UserDalStub([new User { Id = 7, Status = true }]),
            new AuthenticationStateDalStub([new UserAuthenticationState { UserId = 7, AuthorizationVersion = 4 }]),
            new UserSessionDalStub([new UserSession
            {
                Id = sessionId, UserId = 7, ExpiresAt = DateTime.UtcNow.AddMinutes(5),
                AbsoluteExpiresAt = DateTime.UtcNow.AddDays(5)
            }]), cache);

        var result = await manager.ValidateAsync(7, sessionId.ToString(), 4);

        Assert.True(result.Success);
        Assert.Equal("4", cache.GetString($"auth-session:{sessionId}"));
    }

    [Fact]
    public async Task RejectsRevokedOrStaleAuthorizationSession()
    {
        var sessionId = Guid.NewGuid();
        var manager = new TokenValidationManager(
            new UserDalStub([new User { Id = 7, Status = true }]),
            new AuthenticationStateDalStub([new UserAuthenticationState { UserId = 7, AuthorizationVersion = 5 }]),
            new UserSessionDalStub([new UserSession
            {
                Id = sessionId, UserId = 7, ExpiresAt = DateTime.UtcNow.AddMinutes(5),
                AbsoluteExpiresAt = DateTime.UtcNow.AddDays(5), RevokedAt = DateTime.UtcNow
            }]), new InMemoryDistributedCacheStub());

        var result = await manager.ValidateAsync(7, sessionId.ToString(), 4);

        Assert.False(result.Success);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CacheLifetimeNeverExtendsBeyondSessionExpiry(bool absoluteExpiresFirst)
    {
        var sessionId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var nearExpiry = now.AddSeconds(20);
        var laterExpiry = now.AddMinutes(5);
        var cache = new InMemoryDistributedCacheStub();
        var manager = new TokenValidationManager(
            new UserDalStub([new User { Id = 7, Status = true }]),
            new AuthenticationStateDalStub([new UserAuthenticationState { UserId = 7, AuthorizationVersion = 4 }]),
            new UserSessionDalStub([new UserSession
            {
                Id = sessionId, UserId = 7,
                ExpiresAt = absoluteExpiresFirst ? laterExpiry : nearExpiry,
                AbsoluteExpiresAt = absoluteExpiresFirst ? nearExpiry : laterExpiry
            }]), cache);

        Assert.True((await manager.ValidateAsync(7, sessionId.ToString(), 4)).Success);
        var cacheExpiry = Assert.IsType<DateTimeOffset>(cache.LastOptions?.AbsoluteExpiration);
        Assert.True(cacheExpiry > now);
        Assert.True(cacheExpiry <= nearExpiry);
    }

    private sealed class UserDalStub(IEnumerable<User>? values = null) : EntityRepositoryStub<User>(values), IUserDal
    {
        public Task<List<OperationClaim>> GetClaimsAsync(User user, CancellationToken cancellationToken = default) => Task.FromResult(new List<OperationClaim>());
    }
    private sealed class AuthenticationStateDalStub(IEnumerable<UserAuthenticationState>? values = null)
        : EntityRepositoryStub<UserAuthenticationState>(values), IUserAuthenticationStateDal
    {
        public Task IncrementAuthorizationVersionsAsync(IReadOnlyCollection<int> userIds, DateTime updatedAt,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
    private sealed class UserSessionDalStub(IEnumerable<UserSession>? values = null)
        : EntityRepositoryStub<UserSession>(values), IUserSessionDal
    {
        public Task<bool> TryRotateAsync(Guid sessionId, Guid replacementSessionId, DateTime revokedAt,
            CancellationToken cancellationToken = default)
        {
            var session = Get(item => item.Id == sessionId && item.RevokedAt == null && item.ExpiresAt > revokedAt);
            if (session is null) return Task.FromResult(false);
            session.RevokedAt = revokedAt;
            session.ReplacedBySessionId = replacementSessionId;
            return Task.FromResult(true);
        }
        public Task<bool> TryRevokeAsync(Guid sessionId, DateTime revokedAt, CancellationToken cancellationToken = default)
        {
            var session = Get(item => item.Id == sessionId && item.RevokedAt == null);
            if (session is null) return Task.FromResult(false);
            session.RevokedAt = revokedAt;
            return Task.FromResult(true);
        }
        public Task<bool> TryMarkReplayDetectedAsync(Guid sessionId, DateTime detectedAt,
            CancellationToken cancellationToken = default) => Task.FromResult(false);
    }

    private sealed class InMemoryDistributedCacheStub : IDistributedCache
    {
        private readonly Dictionary<string, byte[]> _entries = new();
        public DistributedCacheEntryOptions? LastOptions { get; private set; }
        public byte[]? Get(string key) => _entries.GetValueOrDefault(key);
        public Task<byte[]?> GetAsync(string key, CancellationToken token = default) => Task.FromResult(Get(key));
        public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
        {
            LastOptions = options;
            _entries[key] = value;
        }
        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options,
            CancellationToken token = default)
        {
            Set(key, value, options);
            return Task.CompletedTask;
        }
        public void Refresh(string key) { }
        public Task RefreshAsync(string key, CancellationToken token = default) => Task.CompletedTask;
        public void Remove(string key) => _entries.Remove(key);
        public Task RemoveAsync(string key, CancellationToken token = default)
        {
            Remove(key);
            return Task.CompletedTask;
        }
        public string? GetString(string key) => _entries.TryGetValue(key, out var value) ? Encoding.UTF8.GetString(value) : null;
    }
}
