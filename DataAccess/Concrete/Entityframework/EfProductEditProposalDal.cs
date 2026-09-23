using Core.DataAccess.EntityFramework;
using DataAccess.Abstract;
using DataAccess.Concrete.Entityframework.Contexts;
using Entities;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DataAccess.Concrete.Entityframework
{
    public class EfProductEditProposalDal : EfEntityRepositoryBase<ProductEditProposal, GuidoraDbContext>, IProductEditProposalDal
    {
        public EfProductEditProposalDal(GuidoraDbContext context) : base(context) { }

        public Task<List<ProductEditProposal>> GetPendingPageAsync(int skip, int take,
            CancellationToken cancellationToken = default) => Context.ProductEditProposals.AsNoTracking()
            .Where(proposal => proposal.Status == ProductEditProposalStatus.Pending)
            .OrderBy(proposal => proposal.CreatedAt).ThenBy(proposal => proposal.Id)
            .Skip(skip).Take(take).ToListAsync(cancellationToken);

        public Task<int> CountPendingAsync(CancellationToken cancellationToken = default) =>
            Context.ProductEditProposals.AsNoTracking()
                .CountAsync(proposal => proposal.Status == ProductEditProposalStatus.Pending, cancellationToken);
    }
}
