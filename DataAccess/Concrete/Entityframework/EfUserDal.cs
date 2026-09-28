using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Core.DataAccess.EntityFramework;
using Core.Entities.Concrete;
using DataAccess.Abstract;
using DataAccess.Concrete.Entityframework.Contexts;
using Microsoft.EntityFrameworkCore;
using System.Threading;

namespace DataAccess.Concrete.Entityframework
{
    public class EfUserDal : EfEntityRepositoryBase<User, GuidoraDbContext>, IUserDal
    {
        public EfUserDal(GuidoraDbContext context)
            : base(context)
        {
        }

        public async Task<List<OperationClaim>> GetClaimsAsync(User user, CancellationToken cancellationToken = default)
        {
            var directClaims = from operationClaim in Context.OperationClaims
                join userOperationClaim in Context.UserOperationClaims
                    on operationClaim.Id equals userOperationClaim.OperationClaimId
                where userOperationClaim.UserId == user.Id
                select new OperationClaim { Id = operationClaim.Id, Name = operationClaim.Name };

            var roleClaims = from operationClaim in Context.OperationClaims
                join roleOperationClaim in Context.RoleOperationClaims
                    on operationClaim.Id equals roleOperationClaim.OperationClaimId
                join role in Context.Roles on roleOperationClaim.RoleId equals role.Id
                join userRole in Context.UserRoles on role.Id equals userRole.RoleId
                where userRole.UserId == user.Id && role.IsActive
                select new OperationClaim { Id = operationClaim.Id, Name = operationClaim.Name };

            return await directClaims.Concat(roleClaims).Distinct().ToListAsync(cancellationToken);
        }
    }
}
