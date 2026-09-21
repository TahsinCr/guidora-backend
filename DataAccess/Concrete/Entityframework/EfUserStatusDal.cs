using Core.DataAccess.EntityFramework;
using Core.Entities.Concrete;
using DataAccess.Abstract;
using DataAccess.Concrete.Entityframework.Contexts;

namespace DataAccess.Concrete.Entityframework
{
    public class EfUserStatusDal : EfEntityRepositoryBase<UserStatus, NorthwindContext>, IUserStatusDal
    {
    }
}
