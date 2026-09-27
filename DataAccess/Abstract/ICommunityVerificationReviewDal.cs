using Core.DataAccess;
using Entities;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DataAccess.Abstract;

public interface ICommunityVerificationReviewDal : IEntityRepository<CommunityVerificationReview>
{
    Task<List<CommunityVerificationReview>> GetPendingPageAsync(int skip, int take,
        CancellationToken cancellationToken = default);
}
