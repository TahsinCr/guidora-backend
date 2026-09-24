using Business.Abstract;
using Business.Concrete;
using Core.Entities.Concrete;
using Core.Utilities.Results;
using DataAccess.Abstract;
using Tests.Core.TestDoubles;

namespace Tests.Business.Security;

public sealed class RoleManagerTests
{
    [Fact]
    public async Task GrantingRolePermissionInvalidatesEveryAssignedUsersSessionsOnce()
    {
        var roles = new RoleDalStub([new Role { Id = 3, IsActive = true }]);
        var roleAssignments = new RoleClaimDalStub();
        var userRoles = new UserRoleDalStub([
            new UserRole { UserId = 8, RoleId = 3 },
            new UserRole { UserId = 9, RoleId = 3 },
            new UserRole { UserId = 8, RoleId = 3 }]);
        var sessionInvalidation = new SessionInvalidationStub();
        var audit = new CommunityAuditDalStub();
        var manager = new RoleManager(roles, new UserDalStub(), userRoles, roleAssignments,
            new OperationClaimDalStub([new OperationClaim { Id = 12, Name = "Product.Update" }]),
            sessionInvalidation, audit, new UnitOfWorkStub());

        var result = await manager.GrantPermissionAsync(3, 12, actorUserId: 99);

        Assert.True(result.Success);
        Assert.Single(roleAssignments.GetAll());
        Assert.Equal([8, 9], sessionInvalidation.InvalidatedUserIds);
        Assert.Equal("Granted", Assert.Single(audit.Entries).Action);
    }

    private sealed class RoleDalStub(IEnumerable<Role>? values = null) : EntityRepositoryStub<Role>(values), IRoleDal;
    private sealed class UserDalStub(IEnumerable<User>? values = null) : EntityRepositoryStub<User>(values), IUserDal
    {
        public List<OperationClaim> GetClaims(User user) => [];
        public void AddClaim(User user, OperationClaim operationClaim) { }
    }
    private sealed class UserRoleDalStub(IEnumerable<UserRole>? values = null)
        : EntityRepositoryStub<UserRole>(values), IUserRoleDal
    {
        public override void Purge(UserRole entity) => base.Purge(entity);
        public override void PurgeRange(IEnumerable<UserRole> entities) => base.PurgeRange(entities);
    }
    private sealed class RoleClaimDalStub(IEnumerable<RoleOperationClaim>? values = null)
        : EntityRepositoryStub<RoleOperationClaim>(values), IRoleOperationClaimDal
    {
        public override void Purge(RoleOperationClaim entity) => base.Purge(entity);
        public override void PurgeRange(IEnumerable<RoleOperationClaim> entities) => base.PurgeRange(entities);
    }
    private sealed class OperationClaimDalStub(IEnumerable<OperationClaim>? values = null)
        : EntityRepositoryStub<OperationClaim>(values), IOperationClaimDal;

    private sealed class SessionInvalidationStub : IUserSessionInvalidationService
    {
        public List<int> InvalidatedUserIds { get; } = [];
        public IResult InvalidateUserSessions(int userId)
        {
            InvalidatedUserIds.Add(userId);
            return new SuccessResult();
        }
        public IResult InvalidateUsersSessions(IReadOnlyCollection<int> userIds)
        {
            InvalidatedUserIds.AddRange(userIds);
            return new SuccessResult();
        }
    }
}
