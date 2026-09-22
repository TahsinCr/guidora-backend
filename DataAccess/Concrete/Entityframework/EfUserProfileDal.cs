using Core.DataAccess.EntityFramework;
using Core.Entities.Concrete;
using DataAccess.Abstract;
using DataAccess.Concrete.Entityframework.Contexts;

namespace DataAccess.Concrete.Entityframework
{
    public class EfUserProfileDal : EfEntityRepositoryBase<UserProfile, GuidoraDbContext>, IUserProfileDal
    {
        public EfUserProfileDal(GuidoraDbContext context) : base(context) { }
    }
}
