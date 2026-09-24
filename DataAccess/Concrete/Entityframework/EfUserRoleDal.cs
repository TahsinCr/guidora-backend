using Core.DataAccess.EntityFramework;
using Core.Entities.Concrete;
using DataAccess.Abstract;
using DataAccess.Concrete.Entityframework.Contexts;

namespace DataAccess.Concrete.Entityframework
{
    public class EfUserRoleDal : EfEntityPurgeRepositoryBase<UserRole, GuidoraDbContext>, IUserRoleDal
    {
        public EfUserRoleDal(GuidoraDbContext context) : base(context) { }
    }
}
