using Core.DataAccess.EntityFramework;
using DataAccess.Abstract;
using DataAccess.Concrete.Entityframework.Contexts;
using Entities;

namespace DataAccess.Concrete.Entityframework
{
    public class EfProductMediaDal : EfEntityRepositoryBase<ProductMedia, NorthwindContext>, IProductMediaDal
    {
    }
}
