using DataAccess.Abstract;
using DataAccess.Concrete.Entityframework.Contexts;
using Entities.DTOs;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DataAccess.Concrete.Entityframework;

public sealed class EfProductVerificationReadDal(GuidoraDbContext context) : IProductVerificationReadDal
{
    public Task<ProductVerificationStateSnapshot?> GetPublishedStateAsync(int productId, int? currentUserId,
        CancellationToken cancellationToken = default)
    {
        var voterId = currentUserId ?? -1;
        return context.Products.AsNoTracking()
            .Where(product => product.ProductId == productId && context.ProductStatuses.Any(status =>
                status.Id == product.ProductStatusId && status.Code == "published"))
            .Select(product => new ProductVerificationStateSnapshot(
                context.ProductVerifications.Any(verification => verification.ProductId == product.ProductId &&
                    verification.RevokedAt == null && context.VerificationTypes.Any(type =>
                        type.Id == verification.VerificationTypeId && type.Code == "admin")),
                context.ProductVerifications.Any(verification => verification.ProductId == product.ProductId &&
                    verification.RevokedAt == null && context.VerificationTypes.Any(type =>
                        type.Id == verification.VerificationTypeId && type.Code == "community")),
                context.ProductVerifications.Any(verification => verification.ProductId == product.ProductId &&
                    verification.RevokedAt != null && context.VerificationTypes.Any(type =>
                        type.Id == verification.VerificationTypeId && type.Code == "community")),
                context.CommunityVerificationReviews.Any(review => review.ProductId == product.ProductId &&
                    review.Status == "pending"),
                context.CommunityVerificationReviews.Any(review => review.ProductId == product.ProductId &&
                    review.Status == "rejected"),
                context.ProductVerificationVotes.Where(vote => vote.ProductId == product.ProductId &&
                    vote.UserId == voterId).Select(vote => (bool?)vote.Approved).FirstOrDefault()))
            .SingleOrDefaultAsync(cancellationToken);
    }
}
