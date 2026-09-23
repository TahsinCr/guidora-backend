using Core.Utilities.Results;
using Entities;
using Entities.DTOs;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Business.Abstract;

public interface IProductDocumentService
{
    Task<IDataResult<List<ProductDocument>>> GetByProductAsync(int productId, CancellationToken cancellationToken = default);
    Task<IResult> CreateAsync(int productId, int actorUserId, ProductDocumentRequest request,
        CancellationToken cancellationToken = default);
    Task<IResult> UpdateAsync(int productId, int documentId, int actorUserId, ProductDocumentRequest request,
        CancellationToken cancellationToken = default);
    Task<IResult> DeleteAsync(int productId, int documentId, int actorUserId, CancellationToken cancellationToken = default);
}
