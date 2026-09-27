using Core.DataAccess.EntityFramework;
using DataAccess.Abstract;
using DataAccess.Concrete.Entityframework.Contexts;
using Entities;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace DataAccess.Concrete.Entityframework;

public sealed class EfCommunityVerificationReviewDal
    : EfEntityRepositoryBase<CommunityVerificationReview, GuidoraDbContext>, ICommunityVerificationReviewDal
{
    private readonly GuidoraDbContext _context;
    public EfCommunityVerificationReviewDal(GuidoraDbContext context) : base(context) => _context = context;
    public Task<List<CommunityVerificationReview>> GetPendingPageAsync(int skip, int take,
        CancellationToken cancellationToken = default) => _context.CommunityVerificationReviews.AsNoTracking()
        .Where(item => item.Status == "pending").OrderBy(item => item.ReachedAt).ThenBy(item => item.Id)
        .Skip(skip).Take(take).ToListAsync(cancellationToken);
}
