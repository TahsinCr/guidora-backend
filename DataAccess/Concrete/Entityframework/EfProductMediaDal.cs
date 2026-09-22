using Core.DataAccess.EntityFramework;
using DataAccess.Abstract;
using DataAccess.Concrete.Entityframework.Contexts;
using Entities;

namespace DataAccess.Concrete.Entityframework
{
    public class EfProductMediaDal : EfEntityRepositoryBase<ProductMedia, GuidoraDbContext>, IProductMediaDal
    {
        public EfProductMediaDal(GuidoraDbContext context) : base(context) { }
    }
}
