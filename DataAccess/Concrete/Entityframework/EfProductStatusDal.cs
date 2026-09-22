using Core.DataAccess.EntityFramework;
using DataAccess.Abstract;
using DataAccess.Concrete.Entityframework.Contexts;
using Entities;

namespace DataAccess.Concrete.Entityframework
{
    public class EfProductStatusDal : EfEntityRepositoryBase<ProductStatus, GuidoraDbContext>, IProductStatusDal
    {
        public EfProductStatusDal(GuidoraDbContext context) : base(context) { }
    }
}
