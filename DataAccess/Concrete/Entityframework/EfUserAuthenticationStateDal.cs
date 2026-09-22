using Core.DataAccess.EntityFramework;
using DataAccess.Abstract;
using DataAccess.Concrete.Entityframework.Contexts;
using Entities;

namespace DataAccess.Concrete.Entityframework
{
    public class EfUserAuthenticationStateDal
        : EfEntityRepositoryBase<UserAuthenticationState, GuidoraDbContext>, IUserAuthenticationStateDal
    {
        public EfUserAuthenticationStateDal(GuidoraDbContext context) : base(context) { }
    }
}
