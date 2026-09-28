using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Business.Abstract;
using Business.Concrete;
using Core.Entities.Concrete;
using Core.Utilities.Security.Hashing;
using DataAccess.Abstract;
using Entities;
using Tests.Core.TestDoubles;

namespace Tests.Business.Security;

public sealed class BootstrapAdminManagerTests
{
    [Fact]
    public async Task CreatesAdminIdentityAndIsIdempotentForExistingEmail()
    {
        var users = new UserDalStub();
        var roles = new RoleDalStub([new Role { Id = 3, Code = "admin", IsActive = true }]);
        var assignments = new UserRoleDalStub();
        var profiles = new UserProfileDalStub();
        var authenticationStates = new AuthenticationStateDalStub();
        var unitOfWork = new UnitOfWorkStub();
        IUserService userService = new UserManager(users, unitOfWork);
        var sut = new BootstrapAdminManager(userService, roles, assignments, profiles,
            authenticationStates, unitOfWork);

        const string password = "abcdefghijklmnopqrstuvwxyzABCDEF";
        var created = await sut.EnsureCreatedAsync("bootstrap-test", "bootstrap@example.test", password);
        var repeated = await sut.EnsureCreatedAsync("bootstrap-test", "bootstrap@example.test", password);

        Assert.True(created.Success);
        Assert.True(repeated.Success);
        var user = Assert.Single(users.GetAll());
        Assert.Equal("bootstrap-test", user.FirstName);
        Assert.Equal("Admin", user.LastName);
        Assert.Equal("bootstrap@example.test", user.Email);
        Assert.True(user.Status);
        Assert.True(HashingHelper.VerifyPasswordHash(password, user.PasswordHash, user.PasswordSalt));
        Assert.Single(assignments.GetAll(), assignment => assignment.UserId == user.Id && assignment.RoleId == 3);
        Assert.Single(profiles.GetAll(), profile => profile.UserId == user.Id && profile.DisplayName == "bootstrap-test");
        Assert.Single(authenticationStates.GetAll(), state => state.UserId == user.Id && state.AuthorizationVersion == 1);
    }

    private sealed class UserDalStub : EntityRepositoryStub<User>, IUserDal
    {
        private int _nextId;

        public override void Add(User entity)
        {
            entity.Id = ++_nextId;
            base.Add(entity);
        }

        public Task<List<OperationClaim>> GetClaimsAsync(User user, CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<OperationClaim>());
    }

    private sealed class RoleDalStub(IEnumerable<Role> roles)
        : EntityRepositoryStub<Role>(roles), IRoleDal;

    private sealed class UserRoleDalStub : EntityRepositoryStub<UserRole>, IUserRoleDal;

    private sealed class UserProfileDalStub : EntityRepositoryStub<UserProfile>, IUserProfileDal;

    private sealed class AuthenticationStateDalStub
        : EntityRepositoryStub<UserAuthenticationState>, IUserAuthenticationStateDal
    {
        public Task IncrementAuthorizationVersionsAsync(IReadOnlyCollection<int> userIds,
            System.DateTime updatedAt, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
