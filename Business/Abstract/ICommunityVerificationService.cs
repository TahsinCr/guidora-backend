using Core.Utilities.Results;
using Entities.DTOs;
using System.Threading;
using System.Threading.Tasks;

namespace Business.Abstract;

public interface ICommunityVerificationService
{
    Task<IResult> VoteAsync(int productId, int userId, CommunityVerificationVoteRequest request,
        CancellationToken cancellationToken = default);
    Task<IResult> RevokeAsync(int productId, int actorUserId, ReviewNoteRequest request,
        CancellationToken cancellationToken = default);
}
