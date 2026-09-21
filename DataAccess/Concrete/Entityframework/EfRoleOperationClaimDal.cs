using Core.DataAccess.EntityFramework;
using Core.Entities.Concrete;
using DataAccess.Abstract;
using DataAccess.Concrete.Entityframework.Contexts;

namespace DataAccess.Concrete.Entityframework
{
    public class EfRoleOperationClaimDal : EfEntityRepositoryBase<RoleOperationClaim, NorthwindContext>, IRoleOperationClaimDal
    {
        public EfRoleOperationClaimDal(NorthwindContext context) : base(context) { }
    }
}
