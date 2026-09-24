using Core.DataAccess.EntityFramework;
using DataAccess.Abstract;
using DataAccess.Concrete.Entityframework.Contexts;
using Entities;

namespace DataAccess.Concrete.Entityframework
{
    public class EfProductMediaDal : EfEntityPurgeRepositoryBase<ProductMedia, GuidoraDbContext>, IProductMediaDal
    {
        public EfProductMediaDal(GuidoraDbContext context) : base(context) { }
    }
}
