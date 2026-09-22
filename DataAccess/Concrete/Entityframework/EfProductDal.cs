using Core.DataAccess.EntityFramework;
using DataAccess.Abstract;
using DataAccess.Concrete.Entityframework.Contexts;
using Entities;

namespace DataAccess.Concrete.Entityframework
{
    public class EfProductDal : EfEntityRepositoryBase<Product, GuidoraDbContext>, IProductDal
    {
        public EfProductDal(GuidoraDbContext context) : base(context) { }
    }
}
