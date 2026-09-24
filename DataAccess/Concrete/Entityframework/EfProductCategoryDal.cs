using Core.DataAccess.EntityFramework;
using DataAccess.Abstract;
using DataAccess.Concrete.Entityframework.Contexts;
using Entities;

namespace DataAccess.Concrete.Entityframework
{
    public class EfProductCategoryDal : EfEntityPurgeRepositoryBase<ProductCategory, GuidoraDbContext>, IProductCategoryDal
    {
        public EfProductCategoryDal(GuidoraDbContext context) : base(context) { }
    }
}
