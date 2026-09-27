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
}
