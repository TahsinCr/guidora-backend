using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System;
using Business.Abstract;
using Business.BusinessAspect.Autofac;
using Business.Utilities;
using Business.ValidationRules.FluentValidation;
using Core.Aspects.Transaction;
using Core.Aspects.Autofac.Validation;
using Core.DataAccess;
using Core.Entities.Concrete;
using Core.Utilities.Results;
using DataAccess.Abstract;
using Entities.DTOs;
using Microsoft.EntityFrameworkCore;

namespace Business.Concrete;

public sealed class RoleManager : IRoleService
{
    private readonly IRoleDal _roleDal;
    private readonly IUserDal _userDal;
    private readonly IUserRoleDal _userRoleDal;
    private readonly IRoleOperationClaimDal _roleClaimDal;
    private readonly IOperationClaimDal _operationClaimDal;
    private readonly IUserOperationClaimDal _userOperationClaimDal;
    private readonly IUserSessionInvalidationService _sessionInvalidation;
    private readonly IAuditLogDal _audit;
    private readonly IUnitOfWork _unitOfWork;

    public RoleManager(IRoleDal roleDal, IUserDal userDal, IUserRoleDal userRoleDal,
        IRoleOperationClaimDal roleClaimDal, IOperationClaimDal operationClaimDal,
        IUserOperationClaimDal userOperationClaimDal,
        IUserSessionInvalidationService sessionInvalidation, IAuditLogDal audit, IUnitOfWork unitOfWork)
    {
        _roleDal = roleDal;
        _userDal = userDal;
        _userRoleDal = userRoleDal;
        _roleClaimDal = roleClaimDal;
        _operationClaimDal = operationClaimDal;
        _userOperationClaimDal = userOperationClaimDal;
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
    public async Task<IDataResult<System.Collections.Generic.List<OperationClaim>>> GetOperationClaimsAsync(
        CancellationToken cancellationToken = default) =>
        new SuccessDataResult<System.Collections.Generic.List<OperationClaim>>(
            (await _operationClaimDal.GetAllAsync(null, cancellationToken)).OrderBy(claim => claim.Name).ToList());

    [SecuredOperationAspect("Role.Manage")]
    [ValidationAspect(typeof(RoleRequestValidator))]
    [TransactionScopeAspect]
    public async Task<IResult> CreateAsync(RoleRequest request, int actorUserId,
        CancellationToken cancellationToken = default)
    {
        var code = request.Code.ToLowerInvariant();
        if (await _roleDal.AnyAsync(role => role.Code == code, cancellationToken))
            return new ConflictResult("A role with this code already exists.");
        if (code is "admin" or "user")
            return new ConflictResult("Built-in role codes are reserved.");
        var role = new Role { Name = request.Name, Code = code, IsActive = true };
        try
        {
            await _roleDal.AddAsync(role, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _audit.AddAsync(AuditLogFactory.Create(actorUserId, nameof(Role), role.Id.ToString(), "Created",
                new { }, new { role.Name, role.Code, role.IsActive }), cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            return new ConflictResult("A role with this code already exists.");
        }
        return new SuccessResult("Role created.");
    }

    [SecuredOperationAspect("Role.Manage")]
    [ValidationAspect(typeof(RoleRequestValidator))]
    [TransactionScopeAspect]
    public async Task<IResult> UpdateAsync(int roleId, RoleRequest request, int actorUserId,
        CancellationToken cancellationToken = default)
    {
        var role = await _roleDal.GetAsync(item => item.Id == roleId, cancellationToken);
        if (role is null) return new NotFoundResult("Role was not found.");
        var code = request.Code.ToLowerInvariant();
        if ((role.Code is "admin" or "user") && role.Code != code)
            return new ConflictResult("The code of a built-in system role cannot be changed.");
        if (await _roleDal.AnyAsync(item => item.Id != roleId && item.Code == code, cancellationToken))
            return new ConflictResult("A role with this code already exists.");
        var oldValues = new { role.Name, role.Code, role.IsActive };
        role.Name = request.Name;
        role.Code = code;
        _roleDal.Update(role);
        await _audit.AddAsync(AuditLogFactory.Create(actorUserId, nameof(Role), role.Id.ToString(), "Updated",
            oldValues, new { role.Name, role.Code, role.IsActive }), cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return new SuccessResult("Role updated.");
    }

    [SecuredOperationAspect("Role.Manage")]
    [TransactionScopeAspect]
    public Task<IResult> DeactivateAsync(int roleId, int actorUserId, CancellationToken cancellationToken = default) =>
        SetRoleActiveAsync(roleId, false, actorUserId, cancellationToken);

    [SecuredOperationAspect("Role.Manage")]
    [TransactionScopeAspect]
    public Task<IResult> ActivateAsync(int roleId, int actorUserId, CancellationToken cancellationToken = default) =>
        SetRoleActiveAsync(roleId, true, actorUserId, cancellationToken);

    private async Task<IResult> SetRoleActiveAsync(int roleId, bool isActive, int actorUserId,
        CancellationToken cancellationToken)
    {
        var role = await _roleDal.GetAsync(item => item.Id == roleId, cancellationToken);
        if (role is null) return new NotFoundResult("Role was not found.");
        if (!isActive && (role.Code is "admin" or "user"))
            return new ConflictResult("Built-in Admin and User roles cannot be deactivated.");
        if (role.IsActive == isActive) return new SuccessResult(isActive ? "Role is already active." : "Role is already inactive.");
        role.IsActive = isActive;
        _roleDal.Update(role);
        await _audit.AddAsync(AuditLogFactory.Create(actorUserId, nameof(Role), role.Id.ToString(),
            isActive ? "Activated" : "Deactivated", new { IsActive = !isActive }, new { IsActive = isActive }), cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return await InvalidateRoleUsersAsync(roleId, cancellationToken);
    }

    [SecuredOperationAspect("Role.Manage")]
    public async Task<IDataResult<System.Collections.Generic.List<OperationClaim>>> GetUserClaimsAsync(int userId,
        CancellationToken cancellationToken = default)
    {
        if (!await _userDal.AnyAsync(user => user.Id == userId && user.Status, cancellationToken))
            return new NotFoundDataResult<System.Collections.Generic.List<OperationClaim>>("Active user was not found.");
        var claims = await _userOperationClaimDal.GetDirectClaimsAsync(userId, cancellationToken);
        return new SuccessDataResult<System.Collections.Generic.List<OperationClaim>>(claims);
    }

    [SecuredOperationAspect("Role.Manage")]
    [TransactionScopeAspect]
    public async Task<IResult> GrantUserClaimAsync(int userId, int operationClaimId, int actorUserId,
        CancellationToken cancellationToken = default)
    {
        if (!await _userDal.AnyAsync(user => user.Id == userId && user.Status, cancellationToken))
            return new NotFoundResult("Active user was not found.");
        var claim = await _operationClaimDal.GetAsync(item => item.Id == operationClaimId, cancellationToken);
        if (claim is null) return new NotFoundResult("Operation claim was not found.");
        if (await _userOperationClaimDal.AnyAsync(item => item.UserId == userId && item.OperationClaimId == operationClaimId,
                cancellationToken)) return new SuccessResult("User already has this direct permission.");
        try
        {
            await _userOperationClaimDal.AddAsync(new UserOperationClaim { UserId = userId, OperationClaimId = operationClaimId }, cancellationToken);
            await _audit.AddAsync(AuditLogFactory.Create(actorUserId, nameof(UserOperationClaim), $"{userId}:{operationClaimId}",
                "Granted", new { }, new { UserId = userId, OperationClaimId = operationClaimId }), cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            return new SuccessResult("User already has this direct permission.");
        }
        var invalidation = await _sessionInvalidation.InvalidateUserSessionsAsync(userId, cancellationToken);
        return invalidation.Success ? new SuccessResult("Direct permission granted and user sessions invalidated.") : invalidation;
    }

    [SecuredOperationAspect("Role.Manage")]
    [TransactionScopeAspect]
    public async Task<IResult> RevokeUserClaimAsync(int userId, int operationClaimId, int actorUserId,
        CancellationToken cancellationToken = default)
    {
        if (!await _userDal.AnyAsync(user => user.Id == userId, cancellationToken))
            return new NotFoundResult("User was not found.");
        var assignment = await _userOperationClaimDal.GetAsync(item => item.UserId == userId &&
            item.OperationClaimId == operationClaimId, cancellationToken);
        if (assignment is null) return new SuccessResult("User does not have this direct permission.");
        _userOperationClaimDal.Purge(assignment);
        await _audit.AddAsync(AuditLogFactory.Create(actorUserId, nameof(UserOperationClaim), $"{userId}:{operationClaimId}",
            "Revoked", new { UserId = userId, OperationClaimId = operationClaimId }, new { }), cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        var invalidation = await _sessionInvalidation.InvalidateUserSessionsAsync(userId, cancellationToken);
        return invalidation.Success ? new SuccessResult("Direct permission revoked and user sessions invalidated.") : invalidation;
    }

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
        var invalidation = await _sessionInvalidation.InvalidateUserSessionsAsync(userId, cancellationToken);
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
            await _unitOfWork.AcquireTransactionLockAsync(7319842101L, cancellationToken);
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
        var invalidation = await _sessionInvalidation.InvalidateUserSessionsAsync(userId, cancellationToken);
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
        var invalidation = await _sessionInvalidation.InvalidateUsersSessionsAsync(userIds, cancellationToken);
        return invalidation.Success ? new SuccessResult("Role permission updated and affected sessions invalidated.") : invalidation;
    }

    private static bool IsUniqueConstraintViolation(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException!)
        {
            var sqlState = current.GetType().GetProperty("SqlState")?.GetValue(current) as string;
            if (sqlState == "23505") return true;
        }
        return false;
    }
}
