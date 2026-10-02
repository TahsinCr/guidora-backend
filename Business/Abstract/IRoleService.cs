using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Core.Entities.Concrete;
using Core.Utilities.Results;
using Entities.DTOs;

namespace Business.Abstract;

public interface IRoleService
{
    Task<IDataResult<List<Role>>> GetActiveAsync(CancellationToken cancellationToken = default);
    Task<IDataResult<List<OperationClaim>>> GetOperationClaimsAsync(CancellationToken cancellationToken = default);
    Task<IResult> CreateAsync(RoleRequest request, int actorUserId, CancellationToken cancellationToken = default);
    Task<IResult> UpdateAsync(int roleId, RoleRequest request, int actorUserId, CancellationToken cancellationToken = default);
    Task<IResult> DeactivateAsync(int roleId, int actorUserId, CancellationToken cancellationToken = default);
    Task<IResult> ActivateAsync(int roleId, int actorUserId, CancellationToken cancellationToken = default);
    Task<IDataResult<List<OperationClaim>>> GetUserClaimsAsync(int userId, CancellationToken cancellationToken = default);
    Task<IResult> GrantUserClaimAsync(int userId, int operationClaimId, int actorUserId, CancellationToken cancellationToken = default);
    Task<IResult> RevokeUserClaimAsync(int userId, int operationClaimId, int actorUserId, CancellationToken cancellationToken = default);
    Task<IDataResult<List<OperationClaim>>> GetPermissionsAsync(int roleId,
        CancellationToken cancellationToken = default);
    Task<IResult> AssignToUserAsync(int userId, int roleId, int actorUserId,
        CancellationToken cancellationToken = default);
    Task<IResult> RemoveFromUserAsync(int userId, int roleId, int actorUserId,
        CancellationToken cancellationToken = default);
    Task<IResult> GrantPermissionAsync(int roleId, int operationClaimId, int actorUserId,
        CancellationToken cancellationToken = default);
    Task<IResult> RevokePermissionAsync(int roleId, int operationClaimId, int actorUserId,
        CancellationToken cancellationToken = default);
}
