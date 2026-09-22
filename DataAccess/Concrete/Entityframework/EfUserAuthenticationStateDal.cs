using Core.DataAccess.EntityFramework;
using DataAccess.Abstract;
using DataAccess.Concrete.Entityframework.Contexts;
using Entities;

namespace DataAccess.Concrete.Entityframework
{
    public class EfUserAuthenticationStateDal
        : EfEntityRepositoryBase<UserAuthenticationState, NorthwindContext>, IUserAuthenticationStateDal
    {
        public EfUserAuthenticationStateDal(NorthwindContext context) : base(context) { }
    }
}
