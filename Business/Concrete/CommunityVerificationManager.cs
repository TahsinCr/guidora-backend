using Business.Abstract;
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

namespace Business.Concrete;

public sealed class CommunityVerificationManager : ICommunityVerificationService
{
    private const int PositiveVoteQuorum = 5;
    private readonly IProductDal _productDal;
    private readonly IProductStatusDal _productStatusDal;
    private readonly IUserDal _userDal;
    private readonly IRoleDal _roleDal;
    private readonly IUserRoleDal _userRoleDal;
    private readonly IProductVerificationVoteDal _voteDal;
    private readonly IProductVerificationDal _verificationDal;
    private readonly IVerificationTypeDal _verificationTypeDal;
    private readonly IAuditLogDal _auditDal;
    private readonly IUnitOfWork _unitOfWork;

    public CommunityVerificationManager(IProductDal productDal, IProductStatusDal productStatusDal,
        IUserDal userDal, IRoleDal roleDal,
        IUserRoleDal userRoleDal, IProductVerificationVoteDal voteDal, IProductVerificationDal verificationDal,
        IVerificationTypeDal verificationTypeDal, IAuditLogDal auditDal, IUnitOfWork unitOfWork)
    {
        _productDal = productDal;
        _productStatusDal = productStatusDal;
        _userDal = userDal;
        _roleDal = roleDal;
        _userRoleDal = userRoleDal;
        _voteDal = voteDal;
        _verificationDal = verificationDal;
        _verificationTypeDal = verificationTypeDal;
        _auditDal = auditDal;
        _unitOfWork = unitOfWork;
    }

    [SecuredOperationAspect("Product.Verify.Community.Vote")]
    [TransactionScopeAspect]
    public async Task<IResult> VoteAsync(int productId, int userId, CommunityVerificationVoteRequest request,
        CancellationToken cancellationToken = default)
    {
        await _unitOfWork.AcquireTransactionLockAsync(7_400_000_000L + productId, cancellationToken);
        var product = await _productDal.GetAsync(item => item.ProductId == productId, cancellationToken);
        if (product is null) return new NotFoundResult("Product was not found.");
        var published = await _productStatusDal.GetAsync(item => item.Code == "published", cancellationToken);
        if (published is null) return new ErrorResult("Published Product status is not configured.");
        if (product.ProductStatusId != published.Id)
            return new ConflictResult("Only published Products can receive Community verification votes.");
        if (product.CreatedByUserId == userId)
            return new ForbiddenResult("Product creators cannot vote on their own Community verification.");
        var user = await _userDal.GetAsync(item => item.Id == userId && item.Status, cancellationToken);
        if (user is null) return new ErrorResult("An active User account is required to vote.");
        var userRole = await _roleDal.GetAsync(role => role.Code == "user" && role.IsActive, cancellationToken);
        if (userRole is null || !await _userRoleDal.AnyAsync(
                assignment => assignment.UserId == userId && assignment.RoleId == userRole.Id, cancellationToken))
            return new ErrorResult("Community verification voting is limited to active User-role members.");

        if (await _voteDal.AnyAsync(vote => vote.ProductId == productId && vote.UserId == userId, cancellationToken))
            return new ConflictResult("A user may cast only one Community verification vote per Product.");
        var type = await _verificationTypeDal.GetAsync(item => item.Code == "community", cancellationToken);
        if (type is null) return new ErrorResult("Community verification type is not configured.");
        if (await _verificationDal.AnyAsync(item => item.ProductId == productId &&
                item.VerificationTypeId == type.Id, cancellationToken))
            return new ConflictResult("Community verification has already been decided for this Product.");

        await _voteDal.AddAsync(new ProductVerificationVote
        {
            ProductId = productId,
            UserId = userId,
            Approved = request.Approved,
            CreatedAt = DateTime.UtcNow
        }, cancellationToken);

        var approvedVotes = await _voteDal.CountAsync(vote => vote.ProductId == productId && vote.Approved,
            cancellationToken) + (request.Approved ? 1 : 0);
        var totalVotes = await _voteDal.CountAsync(vote => vote.ProductId == productId, cancellationToken) + 1;
        if (approvedVotes >= PositiveVoteQuorum)
        {
            var verification = new ProductVerification
            {
                ProductId = productId,
                VerificationTypeId = type.Id,
                VerifiedAt = DateTime.UtcNow,
                Note = $"Community verification accepted with {approvedVotes} affirmative votes from {totalVotes} total votes; V1 quorum is {PositiveVoteQuorum}."
            };
            await _verificationDal.AddAsync(verification, cancellationToken);
            await _auditDal.AddAsync(AuditLogFactory.Create(null, nameof(ProductVerification), productId.ToString(),
                "CommunityVerified", new { Status = "Pending" }, new
                {
                    Status = "Active", ApprovedVotes = approvedVotes, TotalVotes = totalVotes,
                    Quorum = PositiveVoteQuorum
                }), cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return new SuccessResult(approvedVotes >= PositiveVoteQuorum
            ? "Community verification quorum reached; Product is verified."
            : $"Vote recorded. {approvedVotes} of {PositiveVoteQuorum} affirmative votes received.");
    }

    [SecuredOperationAspect("Product.Verify")]
    [ValidationAspect(typeof(ReviewNoteRequestValidator))]
    [TransactionScopeAspect]
    public async Task<IResult> RevokeAsync(int productId, int actorUserId, ReviewNoteRequest request,
        CancellationToken cancellationToken = default)
    {
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
