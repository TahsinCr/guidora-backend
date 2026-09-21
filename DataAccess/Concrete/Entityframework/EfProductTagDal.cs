using Core.DataAccess.EntityFramework;
using DataAccess.Abstract;
using DataAccess.Concrete.Entityframework.Contexts;
using Entities;

namespace DataAccess.Concrete.Entityframework
{
    public class EfProductTagDal : EfEntityRepositoryBase<ProductTag, NorthwindContext>, IProductTagDal
    {
        public EfProductTagDal(NorthwindContext context) : base(context) { }
    }
}
