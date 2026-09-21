using Core.DataAccess.EntityFramework;
using Core.Entities.Concrete;
using DataAccess.Abstract;
using DataAccess.Concrete.Entityframework.Contexts;

namespace DataAccess.Concrete.Entityframework
{
    public class EfUserRoleDal : EfEntityRepositoryBase<UserRole, NorthwindContext>, IUserRoleDal
    {
        public EfUserRoleDal(NorthwindContext context) : base(context) { }
    }
}
