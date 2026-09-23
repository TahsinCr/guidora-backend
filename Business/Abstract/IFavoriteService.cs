using Core.Utilities.Results;
using Entities;
using Entities.DTOs;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Business.Abstract;

public interface IFavoriteService
{
    Task<IDataResult<PagedResult<Favorite>>> GetForUserAsync(int userId, PageRequest request,
        CancellationToken cancellationToken = default);
    Task<IResult> AddAsync(int productId, int userId, CancellationToken cancellationToken = default);
    Task<IResult> RemoveAsync(int productId, int userId, CancellationToken cancellationToken = default);
}
