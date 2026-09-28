using Core.DataAccess;
using Entities;
using Entities.DTOs;
using System.Threading;
using System.Threading.Tasks;
using Core.Pagination;

namespace DataAccess.Abstract
{
    public interface IProductChangelogDal : IEntityRepository<ProductChangelog>
    {
        Task<PagedResult<ProductChangelogListItemDto>> GetPageAsync(int productId, int page, int pageSize,
            CancellationToken cancellationToken = default);
    }
}
