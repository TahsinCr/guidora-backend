using Core.DataAccess;
using Entities;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DataAccess.Abstract
{
    public interface IProductDal : IEntityRepository<Product>
    {
        Task<List<Product>> GetPublishedPageAsync(int statusId, int? categoryId, int skip, int take,
            CancellationToken cancellationToken = default);
        Task<int> CountPublishedAsync(int statusId, int? categoryId,
            CancellationToken cancellationToken = default);
    }
}
