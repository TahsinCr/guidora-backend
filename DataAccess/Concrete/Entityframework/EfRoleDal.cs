using Core.DataAccess.EntityFramework;
using Core.Entities.Concrete;
using DataAccess.Abstract;
using DataAccess.Concrete.Entityframework.Contexts;

namespace DataAccess.Concrete.Entityframework
{
    public class EfRoleDal : EfEntityRepositoryBase<Role, GuidoraDbContext>, IRoleDal
    {
        public EfRoleDal(GuidoraDbContext context) : base(context) { }
    }
}
