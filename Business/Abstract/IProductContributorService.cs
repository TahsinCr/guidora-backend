using Core.Utilities.Results;
using Entities;
using Entities.DTOs;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Business.Abstract;

public interface IProductContributorService
{
    Task<IDataResult<List<ProductContributor>>> GetByProductAsync(int productId, CancellationToken cancellationToken = default);
    Task<IResult> AddAsync(int productId, int actorUserId, ProductContributorRequest request,
        CancellationToken cancellationToken = default);
    Task<IResult> RemoveAsync(int productId, int userId, int actorUserId, CancellationToken cancellationToken = default);
}
