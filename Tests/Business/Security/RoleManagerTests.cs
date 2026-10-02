using Business.Abstract;
using Business.Concrete;
using Core.Entities.Concrete;
using Core.Utilities.Results;
using DataAccess.Abstract;
using Entities.DTOs;
using Tests.Core.TestDoubles;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;

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
            new UserOperationClaimDalStub(), sessionInvalidation, audit, new UnitOfWorkStub());

        var result = await manager.GrantPermissionAsync(3, 12, actorUserId: 99);

        Assert.True(result.Success);
        Assert.Single(roleAssignments.GetAll());
        Assert.Equal([8, 9], sessionInvalidation.InvalidatedUserIds);
        Assert.Equal("Granted", Assert.Single(audit.Entries).Action);
    }

    [Fact]
    public async Task CreatingAndUpdatingRoleWritesAuditAndRejectsBuiltInCodeChange()
    {
        var roles = new RoleDalStub([new Role { Id = 1, Name = "Admin", Code = "admin", IsActive = true }]);
        var audit = new CommunityAuditDalStub();
        var manager = CreateManager(roles, audit: audit);

        var created = await manager.CreateAsync(new RoleRequest { Name = "Reviewer", Code = "reviewer" }, 99);
        var denied = await manager.UpdateAsync(1,
            new RoleRequest { Name = "Root", Code = "root" }, 99);

        Assert.True(created.Success);
        Assert.Equal("reviewer", Assert.Single(roles.GetAll(item => item.Code == "reviewer")).Code);
        Assert.Equal("Created", Assert.Single(audit.Entries).Action);
        Assert.False(denied.Success);
        Assert.Equal("admin", roles.Get(item => item.Id == 1)!.Code);
    }

    [Fact]
    public async Task DirectUserClaimGrantAndRevokeAuditAndInvalidateTheTargetUser()
    {
        var userClaims = new UserOperationClaimDalStub();
        var sessions = new SessionInvalidationStub();
        var audit = new CommunityAuditDalStub();
        var manager = CreateManager(userClaims: userClaims, sessions: sessions, audit: audit,
            users: [new User { Id = 8, Status = true }], claims: [new OperationClaim { Id = 12, Name = "Product.Update" }]);

        var granted = await manager.GrantUserClaimAsync(8, 12, 99);
        var revoked = await manager.RevokeUserClaimAsync(8, 12, 99);

        Assert.True(granted.Success);
        Assert.True(revoked.Success);
        Assert.Equal([8, 8], sessions.InvalidatedUserIds);
        Assert.Equal(new[] { "Granted", "Revoked" }, audit.Entries.Select(item => item.Action));
    }

    [Fact]
    public async Task DirectUserClaimGrantIsIdempotentAndRejectsInactiveUsers()
    {
        var userClaims = new UserOperationClaimDalStub();
        var sessions = new SessionInvalidationStub();
        var manager = CreateManager(userClaims: userClaims, sessions: sessions,
            users: [new User { Id = 8, Status = false }], claims: [new OperationClaim { Id = 12, Name = "Product.Update" }]);

        var inactiveUser = await manager.GrantUserClaimAsync(8, 12, 99);

        Assert.False(inactiveUser.Success);
        Assert.Empty(userClaims.GetAll());
        Assert.Empty(sessions.InvalidatedUserIds);
    }

    [Fact]
    public async Task DeactivatingBuiltInRoleIsRejectedAndOtherRoleInvalidatesAssignedUsers()
    {
        var roles = new RoleDalStub([
            new Role { Id = 1, Code = "admin", IsActive = true },
            new Role { Id = 4, Code = "reviewer", IsActive = true }]);
        var users = new UserRoleDalStub([new UserRole { UserId = 8, RoleId = 4 }]);
        var sessions = new SessionInvalidationStub();
        var manager = CreateManager(roles, userRoles: users, sessions: sessions);

        var rejected = await manager.DeactivateAsync(1, 99);
        var deactivated = await manager.DeactivateAsync(4, 99);

        Assert.False(rejected.Success);
        Assert.True(deactivated.Success);
        Assert.False(roles.Get(item => item.Id == 4)!.IsActive);
        Assert.Equal([8], sessions.InvalidatedUserIds);
    }

    private static RoleManager CreateManager(RoleDalStub? roles = null, UserRoleDalStub? userRoles = null,
        UserOperationClaimDalStub? userClaims = null, SessionInvalidationStub? sessions = null,
        CommunityAuditDalStub? audit = null, IEnumerable<User>? users = null, IEnumerable<OperationClaim>? claims = null) =>
        new(roles ?? new RoleDalStub(), new UserDalStub(users), userRoles ?? new UserRoleDalStub(),
            new RoleClaimDalStub(), new OperationClaimDalStub(claims), userClaims ?? new UserOperationClaimDalStub(),
            sessions ?? new SessionInvalidationStub(), audit ?? new CommunityAuditDalStub(), new UnitOfWorkStub());

    private sealed class RoleDalStub(IEnumerable<Role>? values = null) : EntityRepositoryStub<Role>(values), IRoleDal;
    private sealed class UserDalStub(IEnumerable<User>? values = null) : EntityRepositoryStub<User>(values), IUserDal
    {
        public Task<List<OperationClaim>> GetClaimsAsync(User user, CancellationToken cancellationToken = default) => Task.FromResult(new List<OperationClaim>());
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
    private sealed class UserOperationClaimDalStub(IEnumerable<UserOperationClaim>? values = null)
        : EntityRepositoryStub<UserOperationClaim>(values), IUserOperationClaimDal
    {
        public Task<List<OperationClaim>> GetDirectClaimsAsync(int userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(GetAll(item => item.UserId == userId).Join(new[] { new OperationClaim { Id = 12, Name = "Product.Update" } },
                assignment => assignment.OperationClaimId, claim => claim.Id, (_, claim) => claim).OrderBy(claim => claim.Name).ToList());
        public override void Purge(UserOperationClaim entity) => base.Purge(entity);
        public override void PurgeRange(IEnumerable<UserOperationClaim> entities) => base.PurgeRange(entities);
    }

    private sealed class SessionInvalidationStub : IUserSessionInvalidationService
    {
        public List<int> InvalidatedUserIds { get; } = [];
        public Task<IResult> InvalidateUserSessionsAsync(int userId, CancellationToken cancellationToken = default)
        {
            InvalidatedUserIds.Add(userId);
            return Task.FromResult<IResult>(new SuccessResult());
        }
        public Task<IResult> InvalidateUsersSessionsAsync(IReadOnlyCollection<int> userIds, CancellationToken cancellationToken = default)
        {
            InvalidatedUserIds.AddRange(userIds);
            return Task.FromResult<IResult>(new SuccessResult());
        }
    }
}
