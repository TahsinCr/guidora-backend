using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Core.DataAccess;
using Core.Entities.Concrete;

namespace DataAccess.Abstract;

public interface IUserOperationClaimDal : IEntityRepository<UserOperationClaim>, IEntityPurgeRepository<UserOperationClaim>
{
    Task<List<OperationClaim>> GetDirectClaimsAsync(int userId, CancellationToken cancellationToken = default);
}
