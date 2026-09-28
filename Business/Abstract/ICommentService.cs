using Core.Utilities.Results;
using Entities;
using Entities.DTOs;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Core.Pagination;

namespace Business.Abstract;

public interface ICommentService
{
    Task<IDataResult<PagedResult<Comment>>> GetVisibleByProductAsync(int productId, PageRequest request,
        CancellationToken cancellationToken = default);
    Task<IResult> CreateAsync(int productId, int userId, CommentRequest request,
        CancellationToken cancellationToken = default);
    Task<IResult> DeleteAsync(int commentId, int actorUserId, CancellationToken cancellationToken = default);
    Task<IResult> SetVisibilityAsync(int commentId, int actorUserId, bool isVisible,
        CancellationToken cancellationToken = default);
    Task<IResult> BlockAsync(int commentId, int actorUserId, CancellationToken cancellationToken = default);
}
