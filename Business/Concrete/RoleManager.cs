using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Business.Abstract;
using Business.BusinessAspect.Autofac;
using Business.Utilities;
using Core.Aspects.Transaction;
using Core.DataAccess;
using Core.Entities.Concrete;
using Core.Utilities.Results;
using DataAccess.Abstract;
using Microsoft.EntityFrameworkCore;

namespace Business.Concrete;

public sealed class RoleManager : IRoleService
{
    private readonly IRoleDal _roleDal;
    private readonly IUserDal _userDal;
    private readonly IUserRoleDal _userRoleDal;
    private readonly IRoleOperationClaimDal _roleClaimDal;
    private readonly IOperationClaimDal _operationClaimDal;
    private readonly IUserSessionInvalidationService _sessionInvalidation;
    private readonly IAuditLogDal _audit;
    private readonly IUnitOfWork _unitOfWork;

    public RoleManager(IRoleDal roleDal, IUserDal userDal, IUserRoleDal userRoleDal,
        IRoleOperationClaimDal roleClaimDal, IOperationClaimDal operationClaimDal,
        IUserSessionInvalidationService sessionInvalidation, IAuditLogDal audit, IUnitOfWork unitOfWork)
    {
        _roleDal = roleDal;
        _userDal = userDal;
        _userRoleDal = userRoleDal;
        _roleClaimDal = roleClaimDal;
        _operationClaimDal = operationClaimDal;
        _sessionInvalidation = sessionInvalidation;
        _audit = audit;
        _unitOfWork = unitOfWork;
    }

    [SecuredOperationAspect("Role.Manage")]
    public async Task<IDataResult<System.Collections.Generic.List<Role>>> GetActiveAsync(
        CancellationToken cancellationToken = default) =>
        new SuccessDataResult<System.Collections.Generic.List<Role>>(
            (await _roleDal.GetAllAsync(role => role.IsActive, cancellationToken)).OrderBy(role => role.Name).ToList());

    [SecuredOperationAspect("Role.Manage")]
    public async Task<IDataResult<System.Collections.Generic.List<OperationClaim>>> GetPermissionsAsync(int roleId,
        CancellationToken cancellationToken = default)
    {
        if (!await _roleDal.AnyAsync(role => role.Id == roleId && role.IsActive, cancellationToken))
            return new NotFoundDataResult<System.Collections.Generic.List<OperationClaim>>("Active role was not found.");
        var claimIds = (await _roleClaimDal.GetAllAsync(item => item.RoleId == roleId, cancellationToken))
            .Select(item => item.OperationClaimId).ToArray();
        var claims = await _operationClaimDal.GetAllAsync(item => claimIds.Contains(item.Id), cancellationToken);
        return new SuccessDataResult<System.Collections.Generic.List<OperationClaim>>(
            claims.OrderBy(item => item.Name).ToList());
    }

    [SecuredOperationAspect("Role.Manage")]
    [TransactionScopeAspect]
    public async Task<IResult> AssignToUserAsync(int userId, int roleId, int actorUserId,
        CancellationToken cancellationToken = default)
    {
        var user = await _userDal.GetAsync(item => item.Id == userId && item.Status, cancellationToken);
        if (user is null) return new NotFoundResult("Active user was not found.");
        var role = await _roleDal.GetAsync(item => item.Id == roleId && item.IsActive, cancellationToken);
        if (role is null) return new NotFoundResult("Active role was not found.");
        if (await _userRoleDal.AnyAsync(item => item.UserId == userId && item.RoleId == roleId, cancellationToken))
            return new SuccessResult("User already has this role.");

        await _userRoleDal.AddAsync(new UserRole { UserId = userId, RoleId = roleId }, cancellationToken);
        await _audit.AddAsync(AuditLogFactory.Create(actorUserId, nameof(UserRole), $"{userId}:{roleId}",
            "Assigned", new { }, new { UserId = userId, RoleId = roleId }), cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        var invalidation = _sessionInvalidation.InvalidateUserSessions(userId);
        return invalidation.Success ? new SuccessResult("Role assigned and user sessions invalidated.") : invalidation;
    }

    [SecuredOperationAspect("Role.Manage")]
    [TransactionScopeAspect]
    public async Task<IResult> RemoveFromUserAsync(int userId, int roleId, int actorUserId,
        CancellationToken cancellationToken = default)
    {
        var assignment = await _userRoleDal.GetAsync(item => item.UserId == userId && item.RoleId == roleId,
            cancellationToken);
        if (assignment is null) return new SuccessResult("User does not have this role.");
        var role = await _roleDal.GetAsync(item => item.Id == roleId, cancellationToken);
        if (role?.Code == "admin")
        {
            var adminUserIds = (await _userRoleDal.GetAllAsync(item => item.RoleId == roleId, cancellationToken))
                .Select(item => item.UserId).Distinct().ToArray();
            var activeAdmins = await _userDal.CountAsync(user => user.Status && adminUserIds.Contains(user.Id), cancellationToken);
            if (activeAdmins <= 1 && await _userDal.AnyAsync(user => user.Id == userId && user.Status, cancellationToken))
                return new ConflictResult("The last active Admin role assignment cannot be removed.");
        }
        _userRoleDal.Purge(assignment);
        await _audit.AddAsync(AuditLogFactory.Create(actorUserId, nameof(UserRole), $"{userId}:{roleId}",
            "Removed", new { UserId = userId, RoleId = roleId }, new { }), cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        var invalidation = _sessionInvalidation.InvalidateUserSessions(userId);
        return invalidation.Success ? new SuccessResult("Role removed and user sessions invalidated.") : invalidation;
    }

    [SecuredOperationAspect("Role.Manage")]
    [TransactionScopeAspect]
    public async Task<IResult> GrantPermissionAsync(int roleId, int operationClaimId, int actorUserId,
        CancellationToken cancellationToken = default)
    {
        if (!await _roleDal.AnyAsync(role => role.Id == roleId && role.IsActive, cancellationToken))
            return new NotFoundResult("Active role was not found.");
        if (!await _operationClaimDal.AnyAsync(claim => claim.Id == operationClaimId, cancellationToken))
            return new NotFoundResult("Operation claim was not found.");
        if (await _roleClaimDal.AnyAsync(item => item.RoleId == roleId && item.OperationClaimId == operationClaimId,
                cancellationToken)) return new SuccessResult("Role already has this permission.");

        await _roleClaimDal.AddAsync(new RoleOperationClaim { RoleId = roleId, OperationClaimId = operationClaimId },
            cancellationToken);
        await _audit.AddAsync(AuditLogFactory.Create(actorUserId, nameof(RoleOperationClaim),
            $"{roleId}:{operationClaimId}", "Granted", new { }, new { RoleId = roleId, OperationClaimId = operationClaimId }),
            cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return await InvalidateRoleUsersAsync(roleId, cancellationToken);
    }

    [SecuredOperationAspect("Role.Manage")]
    [TransactionScopeAspect]
    public async Task<IResult> RevokePermissionAsync(int roleId, int operationClaimId, int actorUserId,
        CancellationToken cancellationToken = default)
    {
        var assignment = await _roleClaimDal.GetAsync(item => item.RoleId == roleId &&
            item.OperationClaimId == operationClaimId, cancellationToken);
        if (assignment is null) return new SuccessResult("Role does not have this permission.");
        var role = await _roleDal.GetAsync(item => item.Id == roleId, cancellationToken);
        var claim = await _operationClaimDal.GetAsync(item => item.Id == operationClaimId, cancellationToken);
        if (role?.Code == "admin" && claim?.Name == "Role.Manage")
            return new ConflictResult("The Role.Manage permission cannot be removed from the Admin role.");
        _roleClaimDal.Purge(assignment);
        await _audit.AddAsync(AuditLogFactory.Create(actorUserId, nameof(RoleOperationClaim),
            $"{roleId}:{operationClaimId}", "Revoked", new { RoleId = roleId, OperationClaimId = operationClaimId }, new { }),
            cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return await InvalidateRoleUsersAsync(roleId, cancellationToken);
    }

    private async Task<IResult> InvalidateRoleUsersAsync(int roleId, CancellationToken cancellationToken)
    {
        var userIds = (await _userRoleDal.GetAllAsync(item => item.RoleId == roleId, cancellationToken))
            .Select(item => item.UserId).Distinct().ToArray();
        if (userIds.Length == 0) return new SuccessResult("Role permission updated; no assigned users required invalidation.");
        var invalidation = _sessionInvalidation.InvalidateUsersSessions(userIds);
        return invalidation.Success ? new SuccessResult("Role permission updated and affected sessions invalidated.") : invalidation;
    }
}
