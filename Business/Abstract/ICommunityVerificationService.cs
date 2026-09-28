using Core.Utilities.Results;
using Entities.DTOs;
using System.Threading;
using System.Threading.Tasks;
using Core.Pagination;

namespace Business.Abstract;

public interface ICommunityVerificationService
{
    Task<IResult> VoteAsync(int productId, int userId, CommunityVerificationVoteRequest request,
        CancellationToken cancellationToken = default, string? originIpHash = null,
        string? originClientHash = null);
    Task<IDataResult<PagedResult<CommunityVerificationReviewDto>>> GetPendingAsync(PageRequest request,
        CancellationToken cancellationToken = default);
    Task<IResult> ApproveAsync(int productId, int actorUserId, ReviewNoteRequest request,
        CancellationToken cancellationToken = default);
    Task<IResult> RejectAsync(int productId, int actorUserId, ReviewNoteRequest request,
        CancellationToken cancellationToken = default);
    Task<IResult> RevokeAsync(int productId, int actorUserId, ReviewNoteRequest request,
        CancellationToken cancellationToken = default);
}
