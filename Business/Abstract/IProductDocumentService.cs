using Core.Utilities.Results;
using Entities;
using Entities.DTOs;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Core.Pagination;

namespace Business.Abstract;

public interface IProductDocumentService
{
    Task<IDataResult<PagedResult<ProductDocumentListItemDto>>> GetByProductAsync(int productId, PageRequest page,
        CancellationToken cancellationToken = default);
    Task<IDataResult<ProductDocument>> GetByIdAsync(int productId, int documentId,
        CancellationToken cancellationToken = default);
    Task<IResult> CreateAsync(int productId, int actorUserId, ProductDocumentRequest request,
        CancellationToken cancellationToken = default);
    Task<IResult> UpdateAsync(int productId, int documentId, int actorUserId, ProductDocumentRequest request,
        CancellationToken cancellationToken = default);
    Task<IResult> DeleteAsync(int productId, int documentId, int actorUserId, CancellationToken cancellationToken = default);
}
