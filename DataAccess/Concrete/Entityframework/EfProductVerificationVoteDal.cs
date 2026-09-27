using Core.DataAccess.EntityFramework;
using DataAccess.Abstract;
using DataAccess.Concrete.Entityframework.Contexts;
using Entities;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Entities.DTOs;

namespace DataAccess.Concrete.Entityframework;

public sealed class EfProductVerificationVoteDal :
    EfEntityRepositoryBase<ProductVerificationVote, GuidoraDbContext>, IProductVerificationVoteDal
{
    private readonly GuidoraDbContext _context;
    public EfProductVerificationVoteDal(GuidoraDbContext context) : base(context) => _context = context;

    public Task<int> CountCurrentlyEligiblePositiveVotesAsync(int productId, DateTime registeredBefore,
        CancellationToken cancellationToken = default) =>
        (from vote in _context.ProductVerificationVotes
         join user in _context.Users on vote.UserId equals user.Id
         join userRole in _context.UserRoles on user.Id equals userRole.UserId
         join role in _context.Roles on userRole.RoleId equals role.Id
         where vote.ProductId == productId && vote.Approved && vote.EligibleAtCast &&
               user.Status && user.EmailVerifiedAt != null && user.CreatedAt <= registeredBefore &&
               role.Code == "user" && role.IsActive
         select vote.Id).Distinct().CountAsync(cancellationToken);

    public Task<Dictionary<int, int>> CountCurrentlyEligiblePositiveVotesByProductsAsync(
        IReadOnlyCollection<int> productIds, DateTime registeredBefore,
        CancellationToken cancellationToken = default) =>
        (from vote in _context.ProductVerificationVotes
         join user in _context.Users on vote.UserId equals user.Id
         join userRole in _context.UserRoles on user.Id equals userRole.UserId
         join role in _context.Roles on userRole.RoleId equals role.Id
         where productIds.Contains(vote.ProductId) && vote.Approved && vote.EligibleAtCast &&
               user.Status && user.EmailVerifiedAt != null && user.CreatedAt <= registeredBefore &&
               role.Code == "user" && role.IsActive
         select new { vote.ProductId, vote.Id }).Distinct()
        .GroupBy(item => item.ProductId)
        .Select(group => new { ProductId = group.Key, Count = group.Count() })
        .ToDictionaryAsync(item => item.ProductId, item => item.Count, cancellationToken);

    public Task<Dictionary<int, CommunityVoteReviewStats>> GetReviewStatsByProductsAsync(
        IReadOnlyCollection<int> productIds, CancellationToken cancellationToken = default) =>
        _context.ProductVerificationVotes.AsNoTracking()
            .Where(vote => productIds.Contains(vote.ProductId))
            .GroupBy(vote => vote.ProductId)
            .Select(group => new CommunityVoteReviewStats(
                group.Key,
                group.Count(vote => vote.Approved),
                group.Count(vote => !vote.Approved),
                group.Count(vote => vote.Approved && vote.EligibleAtCast),
                group.Count(vote => !vote.Approved && vote.EligibleAtCast),
                group.Where(vote => vote.Approved && vote.EligibleAtCast && vote.OriginIpHash != null)
                    .Select(vote => vote.OriginIpHash).Distinct().Count(),
                group.Count(vote => vote.Approved && vote.EligibleAtCast && vote.OriginIpHash == null),
                group.Where(vote => vote.Approved && vote.EligibleAtCast && vote.OriginClientHash != null)
                    .Select(vote => vote.OriginClientHash).Distinct().Count(),
                group.Count(vote => vote.Approved && vote.EligibleAtCast && vote.OriginClientHash == null)))
            .ToDictionaryAsync(item => item.ProductId, cancellationToken);
}
