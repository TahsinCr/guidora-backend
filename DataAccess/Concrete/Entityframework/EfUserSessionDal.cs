using Core.DataAccess.EntityFramework;
using DataAccess.Abstract;
using DataAccess.Concrete.Entityframework.Contexts;
using Entities;

namespace DataAccess.Concrete.Entityframework
{
    public class EfUserSessionDal : EfEntityRepositoryBase<UserSession, NorthwindContext>, IUserSessionDal
    {
        public EfUserSessionDal(NorthwindContext context) : base(context) { }
    }
}
