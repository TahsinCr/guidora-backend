using Core.DataAccess;
using Entities;

namespace DataAccess.Abstract
{
    public interface IProductCategoryDal : IEntityRepository<ProductCategory>, IEntityPurgeRepository<ProductCategory>
    {
    }
}
