using Core.DataAccess;
using Entities;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DataAccess.Abstract
{
    public interface IProductEditProposalDal : IEntityRepository<ProductEditProposal>
    {
        async Task<List<ProductEditProposal>> GetPendingPageAsync(int skip, int take,
            CancellationToken cancellationToken = default) => (await GetAllAsync(
                proposal => proposal.Status == ProductEditProposalStatus.Pending, cancellationToken))
            .OrderBy(proposal => proposal.CreatedAt).ThenBy(proposal => proposal.Id)
            .Skip(skip).Take(take).ToList();
        Task<int> CountPendingAsync(CancellationToken cancellationToken = default) =>
            CountAsync(proposal => proposal.Status == ProductEditProposalStatus.Pending, cancellationToken);
    }
}
