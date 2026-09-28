using System.Collections.Generic;
using Core.Utilities.Results;
using Entities;
using Entities.DTOs;
using System.Threading;
using System.Threading.Tasks;
using Core.Pagination;

namespace Business.Abstract
{
    public interface IProductService
    {
        Task<IDataResult<Product>> GetByIdAsync(int id, CancellationToken cancellationToken = default);
        Task<IDataResult<PagedResult<ProductListItemDto>>> GetPageAsync(PageRequest request, int? categoryId = null,
            CancellationToken cancellationToken = default);
        Task<IResult> CreateAsync(CreateProductRequest request, int createdByUserId,
            CancellationToken cancellationToken = default);
        Task<IResult> UpdateAsync(int productId, UpdateProductRequest request, int updatedByUserId,
            CancellationToken cancellationToken = default);
        Task<IResult> DeleteAsync(int productId, int deletedByUserId,
            CancellationToken cancellationToken = default);

    }
}
