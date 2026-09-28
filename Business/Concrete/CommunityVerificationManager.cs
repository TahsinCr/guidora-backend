using Business.Abstract;
using Business.Constants;
using Business.BusinessAspect.Autofac;
using Business.Utilities;
using Core.Aspects.Transaction;
using Core.Aspects.Autofac.Validation;
using Core.DataAccess;
using Core.Utilities.Results;
using DataAccess.Abstract;
using Entities;
using Entities.DTOs;
using Business.ValidationRules.FluentValidation;
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Collections.Generic;
using Core.Pagination;

namespace Business.Concrete;

public sealed class CommunityVerificationManager : ICommunityVerificationService
{
    private const int PositiveVoteQuorum = CommunityVerificationPolicy.PositiveVoteQuorum;
    private static readonly TimeSpan MinimumAccountAge =
        TimeSpan.FromDays(CommunityVerificationPolicy.MinimumAccountAgeDays);
    private readonly IProductDal _productDal;
    private readonly IProductStatusDal _productStatusDal;
    private readonly IUserDal _userDal;
    private readonly IRoleDal _roleDal;
    private readonly IUserRoleDal _userRoleDal;
    private readonly IProductVerificationVoteDal _voteDal;
    private readonly IProductVerificationDal _verificationDal;
    private readonly ICommunityVerificationReviewDal _reviewDal;
    private readonly IVerificationTypeDal _verificationTypeDal;
    private readonly IAuditLogDal _auditDal;
    private readonly IUnitOfWork _unitOfWork;

    public CommunityVerificationManager(IProductDal productDal, IProductStatusDal productStatusDal,
        IUserDal userDal, IRoleDal roleDal,
        IUserRoleDal userRoleDal, IProductVerificationVoteDal voteDal, IProductVerificationDal verificationDal,
        ICommunityVerificationReviewDal reviewDal,
        IVerificationTypeDal verificationTypeDal, IAuditLogDal auditDal, IUnitOfWork unitOfWork)
    {
        _productDal = productDal;
        _productStatusDal = productStatusDal;
        _userDal = userDal;
        _roleDal = roleDal;
        _userRoleDal = userRoleDal;
        _voteDal = voteDal;
        _verificationDal = verificationDal;
        _reviewDal = reviewDal;
        _verificationTypeDal = verificationTypeDal;
        _auditDal = auditDal;
        _unitOfWork = unitOfWork;
    }

    [SecuredOperationAspect("Product.Verify.Community.Vote")]
    [TransactionScopeAspect]
    public async Task<IResult> VoteAsync(int productId, int userId, CommunityVerificationVoteRequest request,
        CancellationToken cancellationToken = default, string? originIpHash = null,
        string? originClientHash = null)
    {
        await _unitOfWork.AcquireTransactionLockAsync(TransactionLockKeys.ForCommunityProduct(productId), cancellationToken);
        var product = await _productDal.GetAsync(item => item.ProductId == productId, cancellationToken);
        if (product is null) return new NotFoundResult("Product was not found.");
        var published = await _productStatusDal.GetAsync(item => item.Code == "published", cancellationToken);
        if (published is null) return new ErrorResult("Published Product status is not configured.");
        if (product.ProductStatusId != published.Id)
            return new NotFoundResult("Product was not found.");
        if (product.CreatedByUserId == userId)
            return new ForbiddenResult("Product creators cannot vote on their own Community verification.");
        var user = await _userDal.GetAsync(item => item.Id == userId && item.Status, cancellationToken);
        if (user is null) return new ForbiddenResult("An active User account is required to vote.");
        if (!user.EmailVerifiedAt.HasValue || user.CreatedAt > DateTime.UtcNow - MinimumAccountAge)
            return new ForbiddenResult("Voting requires a verified email and an account at least seven days old.");
        var userRole = await _roleDal.GetAsync(role => role.Code == "user" && role.IsActive, cancellationToken);
        if (userRole is null || !await _userRoleDal.AnyAsync(
                assignment => assignment.UserId == userId && assignment.RoleId == userRole.Id, cancellationToken))
            return new ForbiddenResult("Community verification voting is limited to active User-role members.");

        if (await _voteDal.AnyAsync(vote => vote.ProductId == productId && vote.UserId == userId, cancellationToken))
            return new ConflictResult("A user may cast only one Community verification vote per Product.");
        var type = await _verificationTypeDal.GetAsync(item => item.Code == "community", cancellationToken);
        if (type is null) return new ErrorResult("Community verification type is not configured.");
        var existingVerification = await _verificationDal.GetAsync(item => item.ProductId == productId &&
            item.VerificationTypeId == type.Id, cancellationToken);
        if (existingVerification is not null && !IsLegacyPolicyRevocation(existingVerification))
            return new ConflictResult("Community verification has already been decided for this Product.");
        var review = await _reviewDal.GetAsync(item => item.ProductId == productId, cancellationToken);
        if (review is { Status: "approved" or "rejected" })
            return new ConflictResult("Community verification review has already been decided for this Product.");

        var approvedVotes = await _voteDal.CountCurrentlyEligiblePositiveVotesAsync(productId,
            DateTime.UtcNow - MinimumAccountAge, cancellationToken) + (request.Approved ? 1 : 0);
        await _voteDal.AddAsync(new ProductVerificationVote
        {
            ProductId = productId,
            UserId = userId,
            Approved = request.Approved,
            EligibleAtCast = true,
            CreatedAt = DateTime.UtcNow,
            OriginIpHash = originIpHash,
            OriginClientHash = originClientHash
        }, cancellationToken);

        if (approvedVotes >= PositiveVoteQuorum && review is null)
        {
            await _reviewDal.AddAsync(new CommunityVerificationReview
            {
                ProductId = productId,
                ReachedAt = DateTime.UtcNow,
                Status = "pending"
            }, cancellationToken);
            await _auditDal.AddAsync(AuditLogFactory.Create(null, nameof(CommunityVerificationReview), productId.ToString(),
                "CommunityReviewQueued", new { Status = "CollectingVotes" }, new
                {
                    Status = "PendingReview", ApprovedVotes = approvedVotes,
                    Quorum = PositiveVoteQuorum
                }), cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return new SuccessResult(approvedVotes >= PositiveVoteQuorum
            ? "Community verification quorum reached; moderator review is pending."
            : $"Vote recorded. {approvedVotes} of {PositiveVoteQuorum} affirmative votes received.");
    }

    [SecuredOperationAspect("Product.Verify")]
    public async Task<IDataResult<PagedResult<CommunityVerificationReviewDto>>> GetPendingAsync(PageRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null || request.Page < 1 || request.PageSize is < 1 or > 100)
            return new ErrorDataResult<PagedResult<CommunityVerificationReviewDto>>("Invalid review page request.");
        var offset = ((long)request.Page - 1) * request.PageSize;
        if (offset > int.MaxValue)
            return new ErrorDataResult<PagedResult<CommunityVerificationReviewDto>>("Review page is out of range.");

        var reviews = await _reviewDal.GetPendingPageAsync((int)offset, request.PageSize, cancellationToken);
        var total = await _reviewDal.CountAsync(item => item.Status == "pending", cancellationToken);
        var productIds = reviews.Select(item => item.ProductId).ToArray();
        var stats = productIds.Length == 0 ? new Dictionary<int, CommunityVoteReviewStats>() :
            await _voteDal.GetReviewStatsByProductsAsync(productIds, cancellationToken);
        var currentlyEligible = productIds.Length == 0 ? new Dictionary<int, int>() :
            await _voteDal.CountCurrentlyEligiblePositiveVotesByProductsAsync(productIds,
                DateTime.UtcNow - MinimumAccountAge, cancellationToken);
        var items = reviews.Select(review =>
        {
            stats.TryGetValue(review.ProductId, out var voteStats);
            return new CommunityVerificationReviewDto(review.ProductId, review.ReachedAt,
                voteStats?.PositiveVotes ?? 0, voteStats?.NegativeVotes ?? 0,
                voteStats?.EligiblePositiveVotesAtCast ?? 0,
                voteStats?.EligibleNegativeVotesAtCast ?? 0,
                currentlyEligible.GetValueOrDefault(review.ProductId),
                voteStats?.DistinctIpSignals ?? 0, voteStats?.VotesWithoutIpSignal ?? 0,
                voteStats?.DistinctClientSignals ?? 0, voteStats?.VotesWithoutClientSignal ?? 0);
        }).ToArray();
        return new SuccessDataResult<PagedResult<CommunityVerificationReviewDto>>(
            new PagedResult<CommunityVerificationReviewDto>(items, request.Page, request.PageSize, total));
    }

    [SecuredOperationAspect("Product.Verify")]
    [ValidationAspect(typeof(ReviewNoteRequestValidator))]
    [TransactionScopeAspect]
    public Task<IResult> ApproveAsync(int productId, int actorUserId, ReviewNoteRequest request,
        CancellationToken cancellationToken = default) =>
        DecideAsync(productId, actorUserId, request, true, cancellationToken);

    [SecuredOperationAspect("Product.Verify")]
    [ValidationAspect(typeof(ReviewNoteRequestValidator))]
    [TransactionScopeAspect]
    public Task<IResult> RejectAsync(int productId, int actorUserId, ReviewNoteRequest request,
        CancellationToken cancellationToken = default) =>
        DecideAsync(productId, actorUserId, request, false, cancellationToken);

    private async Task<IResult> DecideAsync(int productId, int actorUserId, ReviewNoteRequest request,
        bool approve, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request?.Note))
            return new ErrorResult("A Community review note is required.");
        await _unitOfWork.AcquireTransactionLockAsync(TransactionLockKeys.ForCommunityProduct(productId), cancellationToken);
        var review = await _reviewDal.GetAsync(item => item.ProductId == productId, cancellationToken);
        if (review is null) return new NotFoundResult("Community verification review was not found.");
        if (review.Status != "pending") return new ConflictResult("Community verification review is already decided.");
        var product = await _productDal.GetAsync(item => item.ProductId == productId, cancellationToken);
        var published = await _productStatusDal.GetAsync(item => item.Code == "published", cancellationToken);
        if (product is null) return new NotFoundResult("Product was not found.");
        if (approve && (published is null || product.ProductStatusId != published.Id))
            return new ConflictResult("Only a published Product can be Community verified.");
        if (product.CreatedByUserId == actorUserId)
            return new ForbiddenResult("Product creators cannot review their own Community verification.");
        if (await _voteDal.AnyAsync(vote => vote.ProductId == productId && vote.UserId == actorUserId,
                cancellationToken))
            return new ForbiddenResult("A voter cannot review the same Product's Community verification.");
        var positiveVotes = await _voteDal.CountCurrentlyEligiblePositiveVotesAsync(productId,
            DateTime.UtcNow - MinimumAccountAge, cancellationToken);
        if (approve && positiveVotes < PositiveVoteQuorum)
            return new ConflictResult("Community verification quorum is no longer met.");

        if (approve)
        {
            var type = await _verificationTypeDal.GetAsync(item => item.Code == "community", cancellationToken);
            if (type is null) return new ErrorResult("Community verification type is not configured.");
            var verification = await _verificationDal.GetAsync(item => item.ProductId == productId &&
                item.VerificationTypeId == type.Id, cancellationToken);
            if (verification is not null && !IsLegacyPolicyRevocation(verification))
                return new ConflictResult("Community verification already exists.");
            if (verification is null)
            {
                verification = new ProductVerification { ProductId = productId, VerificationTypeId = type.Id };
                await _verificationDal.AddAsync(verification, cancellationToken);
            }
            else _verificationDal.Update(verification);
            verification.VerifiedByUserId = actorUserId;
            verification.VerifiedAt = DateTime.UtcNow;
            verification.RevokedAt = null;
            verification.Note = request.Note;
            await _auditDal.AddAsync(AuditLogFactory.Create(actorUserId, nameof(ProductVerification),
                productId.ToString(), "CommunityVerifiedAfterReview", new { Status = "PendingReview" },
                new { Status = "Active", positiveVotes, ReviewNote = request.Note }), cancellationToken);
        }

        review.Status = approve ? "approved" : "rejected";
        review.ReviewedByUserId = actorUserId;
        review.ReviewedAt = DateTime.UtcNow;
        review.Note = request.Note;
        _reviewDal.Update(review);
        await _auditDal.AddAsync(AuditLogFactory.Create(actorUserId, nameof(CommunityVerificationReview),
            productId.ToString(), approve ? "CommunityReviewApproved" : "CommunityReviewRejected",
            new { Status = "PendingReview" }, new { review.Status, review.Note, review.ReviewedAt, positiveVotes }),
            cancellationToken);
        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new ConflictResult("Community verification review changed concurrently.");
        }
        return new SuccessResult(approve ? "Community verification approved." : "Community verification rejected.");
    }

    private static bool IsLegacyPolicyRevocation(ProductVerification verification) =>
        verification.RevokedAt.HasValue && verification.Note ==
        CommunityVerificationPolicy.LegacyPolicyRevocationNote;

    [SecuredOperationAspect("Product.Verify")]
    [ValidationAspect(typeof(ReviewNoteRequestValidator))]
    [TransactionScopeAspect]
    public async Task<IResult> RevokeAsync(int productId, int actorUserId, ReviewNoteRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request?.Note))
            return new ErrorResult("A Community revocation note is required.");
        var type = await _verificationTypeDal.GetAsync(item => item.Code == "community", cancellationToken);
        if (type is null) return new ErrorResult("Community verification type is not configured.");
        var verification = await _verificationDal.GetAsync(item => item.ProductId == productId &&
            item.VerificationTypeId == type.Id, cancellationToken);
        if (verification is null) return new NotFoundResult("Community verification was not found.");
        if (verification.RevokedAt.HasValue) return new ConflictResult("Community verification is already revoked.");
        var oldNote = verification.Note;
        verification.RevokedAt = DateTime.UtcNow;
        verification.Note = request.Note;
        _verificationDal.Update(verification);
        await _auditDal.AddAsync(AuditLogFactory.Create(actorUserId, nameof(ProductVerification), productId.ToString(),
            "CommunityVerificationRevoked", new { Status = "Active", Note = oldNote }, new
            {
                Status = "Revoked", verification.RevokedAt, Note = request.Note
            }), cancellationToken);
        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new ConflictResult("Community verification changed concurrently. Reload and try again.");
        }
        return new SuccessResult("Community verification revoked by a moderator or Admin.");
    }
}
