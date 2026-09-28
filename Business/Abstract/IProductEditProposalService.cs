using Entities.DTOs;
using Core.Utilities.Results;
using Entities;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Core.Pagination;

namespace Business.Abstract;

public interface IProductEditProposalService
{
    Task<IResult> AcceptAsync(int proposalId, int reviewerUserId, ReviewNoteRequest request,
        CancellationToken cancellationToken = default);
    Task<IResult> RejectAsync(int proposalId, int reviewerUserId, ReviewNoteRequest request,
        CancellationToken cancellationToken = default);
    Task<IDataResult<PagedResult<ProductEditProposal>>> GetPendingAsync(PageRequest request,
        CancellationToken cancellationToken = default);
}
