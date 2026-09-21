using Core.DataAccess.EntityFramework;
using DataAccess.Abstract;
using DataAccess.Concrete.Entityframework.Contexts;
using Entities;

namespace DataAccess.Concrete.Entityframework
{
    public class EfProductVerificationDal : EfEntityRepositoryBase<ProductVerification, NorthwindContext>, IProductVerificationDal
    {
        public EfProductVerificationDal(NorthwindContext context) : base(context) { }
    }
}
