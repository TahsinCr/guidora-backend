using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Core.DataAccess.EntityFramework;
using Core.Entities.Concrete;
using DataAccess.Abstract;
using DataAccess.Concrete.Entityframework.Contexts;
using Microsoft.EntityFrameworkCore;

namespace DataAccess.Concrete.Entityframework;

public sealed class EfUserOperationClaimDal : EfEntityPurgeRepositoryBase<UserOperationClaim, GuidoraDbContext>, IUserOperationClaimDal
{
    public EfUserOperationClaimDal(GuidoraDbContext context) : base(context) { }

    public Task<List<OperationClaim>> GetDirectClaimsAsync(int userId, CancellationToken cancellationToken = default) =>
        (from claim in Context.OperationClaims.AsNoTracking()
         join assignment in Context.UserOperationClaims.AsNoTracking() on claim.Id equals assignment.OperationClaimId
         where assignment.UserId == userId
         orderby claim.Name
         select new OperationClaim { Id = claim.Id, Name = claim.Name })
        .ToListAsync(cancellationToken);
}
