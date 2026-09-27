using System;
using Business.Abstract;
using Business.Constants;
using Business.BusinessAspect.Autofac;
using Business.Utilities;
using Business.ValidationRules.FluentValidation;
using Core.Aspects.Autofac.Validation;
using Core.Aspects.Transaction;
using Core.DataAccess;
using Core.Utilities.Results;
using DataAccess.Abstract;
using Entities;
using Entities.DTOs;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Business.Concrete;

public sealed class ProductVerificationManager : IProductVerificationService
{
    private readonly IProductDal _productDal;
    private readonly IProductStatusDal _productStatusDal;
    private readonly IVerificationTypeDal _verificationTypeDal;
    private readonly IProductVerificationDal _verificationDal;
    private readonly IProductVerificationVoteDal _voteDal;
    private readonly ICommunityVerificationReviewDal _reviewDal;
    private readonly IAuditLogDal _auditLogDal;
    private readonly IUnitOfWork _unitOfWork;

    public ProductVerificationManager(IProductDal productDal, IProductStatusDal productStatusDal,
        IVerificationTypeDal verificationTypeDal, IProductVerificationDal verificationDal,
        IProductVerificationVoteDal voteDal, ICommunityVerificationReviewDal reviewDal,
        IAuditLogDal auditLogDal, IUnitOfWork unitOfWork)
    {
        _productDal = productDal;
        _productStatusDal = productStatusDal;
        _verificationTypeDal = verificationTypeDal;
        _verificationDal = verificationDal;
        _voteDal = voteDal;
        _reviewDal = reviewDal;
        _auditLogDal = auditLogDal;
        _unitOfWork = unitOfWork;
    }

    public async Task<IDataResult<ProductVerificationSummaryDto>> GetSummaryAsync(int productId, int? currentUserId,
        CancellationToken cancellationToken = default)
    {
        var published = await _productStatusDal.GetAsync(item => item.Code == "published", cancellationToken);
        if (published is null)
            return new ErrorDataResult<ProductVerificationSummaryDto>("Published Product status is not configured.");
        if (!await _productDal.AnyAsync(item => item.ProductId == productId &&
                item.ProductStatusId == published.Id, cancellationToken))
            return new NotFoundDataResult<ProductVerificationSummaryDto>("Product was not found.");

        var adminType = await _verificationTypeDal.GetAsync(item => item.Code == "admin", cancellationToken);
        var communityType = await _verificationTypeDal.GetAsync(item => item.Code == "community", cancellationToken);
        if (adminType is null || communityType is null)
            return new ErrorDataResult<ProductVerificationSummaryDto>("Product verification types are not configured.");

        var admin = await _verificationDal.GetAsync(item => item.ProductId == productId &&
            item.VerificationTypeId == adminType.Id, cancellationToken);
        var community = await _verificationDal.GetAsync(item => item.ProductId == productId &&
            item.VerificationTypeId == communityType.Id, cancellationToken);
        var review = await _reviewDal.GetAsync(item => item.ProductId == productId, cancellationToken);
        var positiveVotes = await _voteDal.CountAsync(item => item.ProductId == productId && item.Approved &&
            item.EligibleAtCast,
            cancellationToken);
        ProductVerificationVote? currentVote = null;
        if (currentUserId.HasValue)
            currentVote = await _voteDal.GetAsync(item => item.ProductId == productId &&
                item.UserId == currentUserId.Value, cancellationToken);

        return new SuccessDataResult<ProductVerificationSummaryDto>(new ProductVerificationSummaryDto(
            admin is { RevokedAt: null }, community is { RevokedAt: null },
            community is { RevokedAt: not null }, review is { Status: "pending" },
            review is { Status: "rejected" }, positiveVotes,
            CommunityVerificationPolicy.PositiveVoteQuorum,
            currentUserId.HasValue ? currentVote is not null : null,
            currentVote?.Approved));
    }

    [SecuredOperationAspect("Product.Verify")]
    [ValidationAspect(typeof(ReviewNoteRequestValidator))]
    [TransactionScopeAspect]
    public async Task<IResult> VerifyAsAdminAsync(int productId, int verifierUserId, ReviewNoteRequest request,
        CancellationToken cancellationToken = default)
    {
        var product = await _productDal.GetAsync(item => item.ProductId == productId, cancellationToken);
        if (product is null) return new NotFoundResult("Product was not found.");
        var type = await _verificationTypeDal.GetAsync(item => item.Code == "admin", cancellationToken);
        if (type is null) return new ErrorResult("Admin verification type is not configured.");

        var verification = await _verificationDal.GetAsync(item =>
            item.ProductId == productId && item.VerificationTypeId == type.Id, cancellationToken);
        if (verification is { RevokedAt: null })
            return new ConflictResult("Product already has an active admin verification.");

        object oldValues = verification is null
            ? new { Status = "None", VerifiedByUserId = (int?)null, VerifiedAt = (DateTime?)null,
                RevokedAt = (DateTime?)null, Note = "" }
            : new
            {
                Status = "Revoked", verification.VerifiedByUserId,
                verification.VerifiedAt, verification.RevokedAt, verification.Note
            };
        if (verification is null)
        {
            verification = new ProductVerification
            {
                ProductId = productId,
                VerificationTypeId = type.Id
            };
            await _verificationDal.AddAsync(verification, cancellationToken);
        }
        else
        {
            verification.RevokedAt = null;
            verification.VerifiedAt = DateTime.UtcNow;
            verification.VerifiedByUserId = verifierUserId;
            verification.Note = request.Note;
            _verificationDal.Update(verification);
        }

        verification.VerifiedAt = DateTime.UtcNow;
        verification.VerifiedByUserId = verifierUserId;
        verification.Note = request.Note;
        await _auditLogDal.AddAsync(AuditLogFactory.Create(verifierUserId, nameof(ProductVerification), productId.ToString(),
            "AdminVerified", oldValues, new
            {
                Status = "Active", verification.VerifiedByUserId,
                verification.VerifiedAt, verification.RevokedAt, verification.Note
            }), cancellationToken);
        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new ConflictResult("Product verification changed concurrently. Reload and try again.");
        }
        return new SuccessResult("Product verified by an administrator.");
    }

    [SecuredOperationAspect("Product.Verify")]
    [ValidationAspect(typeof(ReviewNoteRequestValidator))]
    [TransactionScopeAspect]
    public async Task<IResult> RevokeAdminVerificationAsync(int productId, int actorUserId, ReviewNoteRequest request,
        CancellationToken cancellationToken = default)
    {
        var type = await _verificationTypeDal.GetAsync(item => item.Code == "admin", cancellationToken);
        if (type is null) return new ErrorResult("Admin verification type is not configured.");
        var verification = await _verificationDal.GetAsync(item =>
            item.ProductId == productId && item.VerificationTypeId == type.Id && item.RevokedAt == null,
            cancellationToken);
        if (verification is null) return new ConflictResult("Product has no active admin verification.");

        var oldValues = new
        {
            Status = "Active", verification.Note, verification.VerifiedByUserId,
            verification.VerifiedAt, verification.RevokedAt
        };
        verification.RevokedAt = DateTime.UtcNow;
        verification.Note = request.Note;
        _verificationDal.Update(verification);
        await _auditLogDal.AddAsync(AuditLogFactory.Create(actorUserId, nameof(ProductVerification), productId.ToString(),
            "AdminVerificationRevoked", oldValues, new
            {
                Status = "Revoked", verification.Note, verification.VerifiedByUserId,
                verification.VerifiedAt, verification.RevokedAt
            }), cancellationToken);
        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new ConflictResult("Product verification changed concurrently. Reload and try again.");
        }
        return new SuccessResult("Admin verification revoked.");
    }
}
