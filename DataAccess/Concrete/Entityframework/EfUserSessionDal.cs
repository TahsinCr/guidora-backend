using Core.DataAccess.EntityFramework;
using DataAccess.Abstract;
using DataAccess.Concrete.Entityframework.Contexts;
using Entities;

namespace DataAccess.Concrete.Entityframework
{
    public class EfUserSessionDal : EfEntityRepositoryBase<UserSession, GuidoraDbContext>, IUserSessionDal
    {
        public EfUserSessionDal(GuidoraDbContext context) : base(context) { }
    }
}
