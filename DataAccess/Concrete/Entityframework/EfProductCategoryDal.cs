using Core.DataAccess.EntityFramework;
using DataAccess.Abstract;
using DataAccess.Concrete.Entityframework.Contexts;
using Entities;

namespace DataAccess.Concrete.Entityframework
{
    public class EfProductCategoryDal : EfEntityRepositoryBase<ProductCategory, NorthwindContext>, IProductCategoryDal
    {
        public EfProductCategoryDal(NorthwindContext context) : base(context) { }
    }
}
