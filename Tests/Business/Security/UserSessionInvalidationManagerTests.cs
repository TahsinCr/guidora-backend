using Business.Concrete;
using Core.DataAccess;
using DataAccess.Abstract;
using Entities;
using Microsoft.Extensions.Caching.Distributed;
using System.Transactions;
using Tests.Core.TestDoubles;

namespace Tests.Business.Security;

public sealed class UserSessionInvalidationManagerTests
{
    [Fact]
    public void InvalidatesOnlyActiveSessionsAndEvictsTheirCacheAfterCommit()
    {
        var state = new UserAuthenticationState { Id = 8, UserId = 42, AuthorizationVersion = 5 };
        var active = new UserSession { Id = Guid.NewGuid(), UserId = 42 };
        var alreadyRevoked = new UserSession { Id = Guid.NewGuid(), UserId = 42, RevokedAt = DateTime.UtcNow };
        var otherUser = new UserSession { Id = Guid.NewGuid(), UserId = 7 };
        var stateDal = new AuthenticationStateDalStub([state]);
        var sessionDal = new UserSessionDalStub([active, alreadyRevoked, otherUser]);
        var cache = new RecordingDistributedCache();
        var unitOfWork = new CommitAwareUnitOfWork(() => cache.CommitObserved = true);
        var sut = new UserSessionInvalidationManager(stateDal, sessionDal, cache, unitOfWork);

        var result = sut.InvalidateUserSessions(42);

        Assert.True(result.Success);
        Assert.Equal(6, state.AuthorizationVersion);
        Assert.Equal(1, stateDal.UpdateCount);
        Assert.Equal(1, sessionDal.UpdateCount);
        Assert.NotNull(active.RevokedAt);
        Assert.NotNull(alreadyRevoked.RevokedAt);
        Assert.Null(otherUser.RevokedAt);
        Assert.Equal([$"auth-session:{active.Id}"], cache.RemovedKeys);
        Assert.True(cache.CommitObservedAtEveryRemoval);
        Assert.Equal(1, unitOfWork.SaveCount);
    }

    [Fact]
    public void CreatesMissingAuthenticationStateWithoutMarkingItAsAnUpdate()
    {
        var stateDal = new AuthenticationStateDalStub();
        var sessionDal = new UserSessionDalStub();
        var unitOfWork = new UnitOfWorkStub();
        var sut = new UserSessionInvalidationManager(stateDal, sessionDal,
            new RecordingDistributedCache(), unitOfWork);

        var result = sut.InvalidateUserSessions(42);

        Assert.True(result.Success);
        var created = Assert.Single(stateDal.Added);
        Assert.Equal(42, created.UserId);
        Assert.Equal(2, created.AuthorizationVersion);
        Assert.Equal(0, stateDal.UpdateCount);
        Assert.Equal(1, unitOfWork.SaveCount);
    }

    [Fact]
    public void BulkInvalidationUpdatesStatesAndSessionsWithOneCommit()
    {
        var first = new UserAuthenticationState { Id = 1, UserId = 42, AuthorizationVersion = 4 };
        var stateDal = new AuthenticationStateDalStub([first]);
        var cache = new RecordingDistributedCache();
        var sessions = new UserSessionDalStub([
            new UserSession { Id = Guid.NewGuid(), UserId = 42 },
            new UserSession { Id = Guid.NewGuid(), UserId = 43 }]);
        var unitOfWork = new UnitOfWorkStub();
        var sut = new UserSessionInvalidationManager(stateDal, sessions, cache, unitOfWork);

        var result = sut.InvalidateUsersSessions([42, 43, 42]);

        Assert.True(result.Success);
        Assert.Equal(5, first.AuthorizationVersion);
        Assert.Equal(1, stateDal.UpdateCount);
        Assert.Single(stateDal.Added, item => item.UserId == 43);
        Assert.Equal(2, sessions.UpdateCount);
        Assert.Equal(1, unitOfWork.SaveCount);
        Assert.Equal(2, cache.RemovedKeys.Count);
    }

    [Fact]
    public void DoesNotEvictSessionCacheWhenPersistenceFails()
    {
        var session = new UserSession { Id = Guid.NewGuid(), UserId = 42 };
        var cache = new RecordingDistributedCache();
        var sut = new UserSessionInvalidationManager(new AuthenticationStateDalStub(),
            new UserSessionDalStub([session]), cache, new FailingUnitOfWork());

        Assert.Throws<InvalidOperationException>(() => sut.InvalidateUserSessions(42));

        Assert.Empty(cache.RemovedKeys);
        Assert.NotNull(session.RevokedAt);
    }

    [Fact]
    public void DefersCacheEvictionUntilAmbientTransactionCommits()
    {
        var session = new UserSession { Id = Guid.NewGuid(), UserId = 42 };
        var cache = new RecordingDistributedCache();
        var sut = CreateManager(session, cache, new UnitOfWorkStub());

        using (var transaction = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
        {
            Assert.True(sut.InvalidateUserSessions(42).Success);
            Assert.Empty(cache.RemovedKeys);
            transaction.Complete();
        }

        Assert.Equal([$"auth-session:{session.Id}"], cache.RemovedKeys);
    }

    [Fact]
    public void DoesNotEvictCacheWhenAmbientTransactionRollsBack()
    {
        var session = new UserSession { Id = Guid.NewGuid(), UserId = 42 };
        var cache = new RecordingDistributedCache();
        var sut = CreateManager(session, cache, new UnitOfWorkStub());

        using (new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
        {
            Assert.True(sut.InvalidateUserSessions(42).Success);
            Assert.Empty(cache.RemovedKeys);
        }

        Assert.Empty(cache.RemovedKeys);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void RejectsInvalidUserIdsWithoutWriting(int userId)
    {
        var unitOfWork = new UnitOfWorkStub();
        var sut = new UserSessionInvalidationManager(new AuthenticationStateDalStub(),
            new UserSessionDalStub(), new RecordingDistributedCache(), unitOfWork);

        var result = sut.InvalidateUserSessions(userId);

        Assert.False(result.Success);
        Assert.Equal(0, unitOfWork.SaveCount);
    }

    private sealed class AuthenticationStateDalStub(IEnumerable<UserAuthenticationState>? items = null)
        : EntityRepositoryStub<UserAuthenticationState>(items), IUserAuthenticationStateDal
    {
        public List<UserAuthenticationState> Added { get; } = [];
        public override void Add(UserAuthenticationState entity)
        {
            Added.Add(entity);
            base.Add(entity);
        }
    }

    private static UserSessionInvalidationManager CreateManager(UserSession session,
        RecordingDistributedCache cache, IUnitOfWork unitOfWork) =>
        new(new AuthenticationStateDalStub(), new UserSessionDalStub([session]), cache, unitOfWork);

    private sealed class UserSessionDalStub(IEnumerable<UserSession>? items = null)
        : EntityRepositoryStub<UserSession>(items), IUserSessionDal;

    private sealed class CommitAwareUnitOfWork(Action onSave) : IUnitOfWork
    {
        public int SaveCount { get; private set; }
        public int SaveChanges()
        {
            SaveCount++;
            onSave();
            return 1;
        }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(SaveChanges());
    }

    private sealed class FailingUnitOfWork : IUnitOfWork
    {
        public int SaveChanges() => throw new InvalidOperationException("Simulated database failure.");
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
            Task.FromException<int>(new InvalidOperationException("Simulated database failure."));
    }

    private sealed class RecordingDistributedCache : IDistributedCache
    {
        public List<string> RemovedKeys { get; } = [];
        public bool CommitObserved { get; set; }
        public bool CommitObservedAtEveryRemoval { get; private set; } = true;

        public byte[]? Get(string key) => null;
        public Task<byte[]?> GetAsync(string key, CancellationToken token = default) => Task.FromResult<byte[]?>(null);
        public void Refresh(string key) { }
        public Task RefreshAsync(string key, CancellationToken token = default) => Task.CompletedTask;
        public void Remove(string key)
        {
            RemovedKeys.Add(key);
            CommitObservedAtEveryRemoval &= CommitObserved;
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
