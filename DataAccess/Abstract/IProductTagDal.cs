using Core.DataAccess;
using Entities;
using System.Threading;
using System.Threading.Tasks;

namespace DataAccess.Abstract
{
    public interface IProductTagDal : IEntityRepository<ProductTag>, IEntityPurgeRepository<ProductTag>
    {
        Task<bool> HasOtherTagInSameCategoryForAssignedProductsAsync(int tagId, int tagCategoryId,
            CancellationToken cancellationToken = default);
    }
}
