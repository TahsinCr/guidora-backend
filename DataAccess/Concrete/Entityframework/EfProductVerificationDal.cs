using Core.DataAccess.EntityFramework;
using DataAccess.Abstract;
using DataAccess.Concrete.Entityframework.Contexts;
using Entities;

namespace DataAccess.Concrete.Entityframework
{
    public class EfProductVerificationDal : EfEntityRepositoryBase<ProductVerification, GuidoraDbContext>, IProductVerificationDal
    {
        public EfProductVerificationDal(GuidoraDbContext context) : base(context) { }
    }
}
