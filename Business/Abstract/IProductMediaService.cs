using Core.Utilities.Results;
using Entities;
using Entities.DTOs;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Business.Abstract;

public interface IProductMediaService
{
    Task<IDataResult<List<ProductMedia>>> GetByProductAsync(int productId, CancellationToken cancellationToken = default);
    Task<IResult> CreateAsync(int productId, int actorUserId, ProductMediaRequest request,
        CancellationToken cancellationToken = default);
    Task<IResult> UpdateAsync(int productId, int mediaId, int actorUserId, ProductMediaRequest request,
        CancellationToken cancellationToken = default);
    Task<IResult> DeleteAsync(int productId, int mediaId, int actorUserId, CancellationToken cancellationToken = default);
}
