using Core.DataAccess;
using Entities;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace DataAccess.Abstract;

public interface IProductVerificationVoteDal : IEntityRepository<ProductVerificationVote>
{
    Task<int> CountCurrentlyEligiblePositiveVotesAsync(int productId, DateTime registeredBefore,
        CancellationToken cancellationToken = default);
    Task<Dictionary<int, int>> CountCurrentlyEligiblePositiveVotesByProductsAsync(
        IReadOnlyCollection<int> productIds, DateTime registeredBefore,
        CancellationToken cancellationToken = default);
}
