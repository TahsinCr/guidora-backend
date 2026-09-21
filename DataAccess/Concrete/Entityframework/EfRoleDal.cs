using Core.DataAccess.EntityFramework;
using Core.Entities.Concrete;
using DataAccess.Abstract;
using DataAccess.Concrete.Entityframework.Contexts;

namespace DataAccess.Concrete.Entityframework
{
    public class EfRoleDal : EfEntityRepositoryBase<Role, NorthwindContext>, IRoleDal
    {
        public EfRoleDal(NorthwindContext context) : base(context) { }
    }
}
