using Core.Utilities.Results;
using Entities;
using Entities.DTOs;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Business.Abstract;

public interface IProductChangelogService
{
    Task<IDataResult<List<ProductChangelog>>> GetByProductAsync(int productId, CancellationToken cancellationToken = default);
    Task<IResult> CreateAsync(int productId, int actorUserId, ProductChangelogRequest request,
        CancellationToken cancellationToken = default);
}
