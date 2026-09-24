using Core.Utilities.Results;
using Entities;
using Entities.DTOs;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Business.Abstract
{
    public interface ICategoryService
    {
        Task<IDataResult<List<Category>>> GetAllAsync(CancellationToken cancellationToken = default);
        Task<IDataResult<Category>> GetByIdAsync(int id, CancellationToken cancellationToken = default);
        Task<IResult> CreateAsync(CategoryRequest request, int createdByUserId,
            CancellationToken cancellationToken = default);
        Task<IResult> UpdateAsync(int categoryId, CategoryRequest request, int updatedByUserId,
            CancellationToken cancellationToken = default);
        Task<IResult> DeactivateAsync(int categoryId, int updatedByUserId,
            CancellationToken cancellationToken = default);
    }
}
