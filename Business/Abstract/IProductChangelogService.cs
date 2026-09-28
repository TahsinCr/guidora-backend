using Core.Utilities.Results;
using Entities;
using Entities.DTOs;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Core.Pagination;

namespace Business.Abstract;

public interface IProductChangelogService
{
    Task<IDataResult<PagedResult<ProductChangelogListItemDto>>> GetByProductAsync(int productId, PageRequest page,
        CancellationToken cancellationToken = default);
    Task<IDataResult<ProductChangelog>> GetByIdAsync(int productId, int changelogId,
        CancellationToken cancellationToken = default);
    Task<IResult> CreateAsync(int productId, int actorUserId, ProductChangelogRequest request,
        CancellationToken cancellationToken = default);
}
