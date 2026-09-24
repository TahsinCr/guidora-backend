using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Core.Entities.Concrete;
using Core.Utilities.Results;

namespace Business.Abstract;

public interface IRoleService
{
    Task<IDataResult<List<Role>>> GetActiveAsync(CancellationToken cancellationToken = default);
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
