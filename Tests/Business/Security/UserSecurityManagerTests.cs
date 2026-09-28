using Business.Abstract;
using Business.Concrete;
using Core.Entities.Concrete;
using Core.Utilities.Results;
using DataAccess.Abstract;
using Tests.Core.TestDoubles;

namespace Tests.Business.Security;

public sealed class UserSecurityManagerTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task MissingUserForBanOrRestoreReturnsNotFoundWithoutInvalidatingSessions(bool ban)
    {
        var invalidation = new SessionInvalidationStub();
        var manager = new UserSecurityManager(new MissingUserService(), invalidation,
            new RoleDalStub(), new UserDalStub(), new UserRoleDalStub(), new UnitOfWorkStub());

        var result = ban
            ? await manager.BanAsync(42)
            : await manager.RestoreAsync(42);

        Assert.IsAssignableFrom<INotFoundResult>(result);
        Assert.Equal(0, invalidation.CallCount);
    }

    private sealed class RoleDalStub : EntityRepositoryStub<Role>, IRoleDal;
    private sealed class UserDalStub : EntityRepositoryStub<User>, IUserDal
    {
        public Task<List<OperationClaim>> GetClaimsAsync(User user, CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<OperationClaim>());
    }
    private sealed class UserRoleDalStub : EntityRepositoryStub<UserRole>, IUserRoleDal;

    private sealed class MissingUserService : IUserService
    {
        public Task<IDataResult<List<OperationClaim>>> GetClaimsAsync(User user,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IDataResult<User>> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
            Task.FromResult<IDataResult<User>>(new ErrorDataResult<User>("User was not found."));

        public Task<IDataResult<User>> GetByMailAsync(string email, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IResult> RegisterUserAsync(User user, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IResult> UpdateUserAsync(User user, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class SessionInvalidationStub : IUserSessionInvalidationService
    {
        public int CallCount { get; private set; }

        public Task<IResult> InvalidateUserSessionsAsync(int userId, CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult<IResult>(new SuccessResult());
        }

        public Task<IResult> InvalidateUsersSessionsAsync(IReadOnlyCollection<int> userIds,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
