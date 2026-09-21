using Core.DataAccess.EntityFramework;
using DataAccess.Abstract;
using DataAccess.Concrete.Entityframework.Contexts;
using Entities;

namespace DataAccess.Concrete.Entityframework
{
    public class EfProductStatusDal : EfEntityRepositoryBase<ProductStatus, NorthwindContext>, IProductStatusDal
    {
        public EfProductStatusDal(NorthwindContext context) : base(context) { }
    }
}
