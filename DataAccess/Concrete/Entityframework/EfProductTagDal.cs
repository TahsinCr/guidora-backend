using Core.DataAccess.EntityFramework;
using DataAccess.Abstract;
using DataAccess.Concrete.Entityframework.Contexts;
using Entities;

namespace DataAccess.Concrete.Entityframework
{
    public class EfProductTagDal : EfEntityRepositoryBase<ProductTag, GuidoraDbContext>, IProductTagDal
    {
        public EfProductTagDal(GuidoraDbContext context) : base(context) { }
    }
}
