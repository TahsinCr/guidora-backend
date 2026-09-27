using Business.Concrete;
using Core.Entities.Concrete;
using Core.Utilities.Results;
using DataAccess.Abstract;
using Entities;
using Entities.DTOs;
using Tests.Core.TestDoubles;

namespace Tests.Business.Governance;

public sealed class CommunityVerificationManagerTests
{
    [Fact]
    public async Task ThirtyEligibleVotesQueueReviewWithoutCreatingBadgeUntilModeratorApproves()
    {
        var users = Enumerable.Range(1, 31).Select(id => new User
        {
            Id = id, Status = true, CreatedAt = DateTime.UtcNow.AddDays(-8),
            EmailVerifiedAt = DateTime.UtcNow.AddDays(-7)
        }).ToArray();
        var votes = new VoteDal();
        var verifications = new VerificationDal();
        var reviews = new ReviewDal();
        var manager = CreateManager(users, votes, verifications, reviews);

        foreach (var user in users.Skip(1).Take(29))
            Assert.True((await manager.VoteAsync(5, user.Id,
                new CommunityVerificationVoteRequest { Approved = true }, originIpHash: "one-ip",
                originClientHash: "one-client")).Success);
        Assert.Empty(reviews.GetAll());
        Assert.Empty(verifications.GetAll());

        Assert.True((await manager.VoteAsync(5, users[30].Id,
            new CommunityVerificationVoteRequest { Approved = true }, originIpHash: "one-ip",
            originClientHash: "one-client")).Success);
        Assert.Equal("pending", Assert.Single(reviews.GetAll()).Status);
        Assert.Empty(verifications.GetAll());
        var pending = await manager.GetPendingAsync(new PageRequest { Page = 1, PageSize = 10 });
        Assert.Equal(30, Assert.Single(pending.Data.Items).PositiveVotes);
        Assert.Equal(30, Assert.Single(pending.Data.Items).CurrentlyEligiblePositiveVotes);
        Assert.Equal(1, Assert.Single(pending.Data.Items).DistinctIpSignals);
        Assert.Equal(1, Assert.Single(pending.Data.Items).DistinctClientSignals);

        Assert.IsAssignableFrom<IForbiddenResult>(await manager.ApproveAsync(5, 1,
            new ReviewNoteRequest { Note = "Own product" }));
        Assert.IsAssignableFrom<IForbiddenResult>(await manager.ApproveAsync(5, users[1].Id,
            new ReviewNoteRequest { Note = "I voted" }));
        Assert.Empty(verifications.GetAll());
        Assert.False((await manager.ApproveAsync(5, 99, new ReviewNoteRequest())).Success);

        Assert.True((await manager.ApproveAsync(5, 99, new ReviewNoteRequest { Note = "Reviewed signals" })).Success);
        Assert.Equal("approved", Assert.Single(reviews.GetAll()).Status);
        Assert.Equal(99, Assert.Single(verifications.GetAll()).VerifiedByUserId);
        Assert.IsAssignableFrom<IConflictResult>(await manager.ApproveAsync(5, 99,
            new ReviewNoteRequest { Note = "Again" }));
    }

    [Fact]
    public async Task UnverifiedOrYoungAccountCannotVoteAndRejectedReviewStaysTerminal()
    {
        var user = new User { Id = 2, Status = true, CreatedAt = DateTime.UtcNow.AddDays(-8) };
        var reviews = new ReviewDal([new CommunityVerificationReview
            { ProductId = 5, Status = "pending", ReachedAt = DateTime.UtcNow }]);
        var votes = new VoteDal(Enumerable.Range(10, 29).Select(id => new ProductVerificationVote
            { ProductId = 5, UserId = id, Approved = true, EligibleAtCast = true }));
        var verifications = new VerificationDal();
        var manager = CreateManager([user], votes, verifications, reviews);

        Assert.IsAssignableFrom<IForbiddenResult>(await manager.VoteAsync(5, user.Id,
            new CommunityVerificationVoteRequest { Approved = true }));
        user.EmailVerifiedAt = DateTime.UtcNow;
        user.CreatedAt = DateTime.UtcNow.AddDays(-6);
        Assert.IsAssignableFrom<IForbiddenResult>(await manager.VoteAsync(5, user.Id,
            new CommunityVerificationVoteRequest { Approved = true }));
        Assert.True((await manager.RejectAsync(5, 99, new ReviewNoteRequest { Note = "Linked accounts" })).Success);
        Assert.Empty(verifications.GetAll());
        user.CreatedAt = DateTime.UtcNow.AddDays(-8);
        Assert.IsAssignableFrom<IConflictResult>(await manager.VoteAsync(5, user.Id,
            new CommunityVerificationVoteRequest { Approved = true }));
    }

    private static CommunityVerificationManager CreateManager(IEnumerable<User> users, VoteDal votes,
        VerificationDal verifications, ReviewDal reviews)
    {
        var assignments = users.Select(user => new UserRole { UserId = user.Id, RoleId = 1 });
        return new CommunityVerificationManager(
            new CommunityProductDalStub([new Product { ProductId = 5, ProductStatusId = 1, CreatedByUserId = 1 }]),
            new CommunityProductStatusDalStub([new ProductStatus { Id = 1, Code = "published" }]),
            new CommunityUserDalStub(users),
            new RoleDal([new Role { Id = 1, Code = "user", IsActive = true }]),
            new UserRoleDal(assignments), votes, verifications, reviews,
            new VerificationTypeDal([new VerificationType { Id = 2, Code = "community" }]),
            new CommunityAuditDalStub(), new UnitOfWorkStub());
    }

    private sealed class RoleDal(IEnumerable<Role> roles) : EntityRepositoryStub<Role>(roles), IRoleDal;
    private sealed class UserRoleDal(IEnumerable<UserRole> roles) : EntityRepositoryStub<UserRole>(roles), IUserRoleDal;
    private sealed class VoteDal(IEnumerable<ProductVerificationVote>? votes = null)
        : EntityRepositoryStub<ProductVerificationVote>(votes), IProductVerificationVoteDal
    {
        public Task<int> CountCurrentlyEligiblePositiveVotesAsync(int productId, DateTime registeredBefore,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(GetAll(item => item.ProductId == productId && item.Approved && item.EligibleAtCast).Count);
        public Task<Dictionary<int, int>> CountCurrentlyEligiblePositiveVotesByProductsAsync(
            IReadOnlyCollection<int> productIds, DateTime registeredBefore,
            CancellationToken cancellationToken = default) => Task.FromResult(GetAll(item =>
                productIds.Contains(item.ProductId) && item.Approved && item.EligibleAtCast)
            .GroupBy(item => item.ProductId).ToDictionary(group => group.Key, group => group.Count()));
        public Task<Dictionary<int, CommunityVoteReviewStats>> GetReviewStatsByProductsAsync(
            IReadOnlyCollection<int> productIds, CancellationToken cancellationToken = default) =>
            Task.FromResult(GetAll(item => productIds.Contains(item.ProductId))
                .GroupBy(item => item.ProductId).ToDictionary(group => group.Key, group =>
                {
                    var eligiblePositive = group.Where(item => item.Approved && item.EligibleAtCast).ToArray();
                    return new CommunityVoteReviewStats(group.Key, group.Count(item => item.Approved),
                        group.Count(item => !item.Approved), eligiblePositive.Length,
                        group.Count(item => !item.Approved && item.EligibleAtCast),
                        eligiblePositive.Where(item => item.OriginIpHash is not null)
                            .Select(item => item.OriginIpHash).Distinct().Count(),
                        eligiblePositive.Count(item => item.OriginIpHash is null),
                        eligiblePositive.Where(item => item.OriginClientHash is not null)
                            .Select(item => item.OriginClientHash).Distinct().Count(),
                        eligiblePositive.Count(item => item.OriginClientHash is null));
                }));
    }
    private sealed class VerificationDal : EntityRepositoryStub<ProductVerification>, IProductVerificationDal;
    private sealed class VerificationTypeDal(IEnumerable<VerificationType> types)
        : EntityRepositoryStub<VerificationType>(types), IVerificationTypeDal;
    private sealed class ReviewDal(IEnumerable<CommunityVerificationReview>? reviews = null)
        : EntityRepositoryStub<CommunityVerificationReview>(reviews), ICommunityVerificationReviewDal
    {
        public Task<List<CommunityVerificationReview>> GetPendingPageAsync(int skip, int take,
            CancellationToken cancellationToken = default) => Task.FromResult(GetAll(item => item.Status == "pending")
            .OrderBy(item => item.ReachedAt).Skip(skip).Take(take).ToList());
    }
}
