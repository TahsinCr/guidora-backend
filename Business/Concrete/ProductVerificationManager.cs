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
    private readonly IVerificationTypeDal _verificationTypeDal;
    private readonly IProductVerificationDal _verificationDal;
    private readonly IProductVerificationVoteDal _voteDal;
    private readonly IProductVerificationReadDal _readDal;
    private readonly IAuditLogDal _auditLogDal;
    private readonly IUnitOfWork _unitOfWork;

    public ProductVerificationManager(IProductDal productDal,
        IVerificationTypeDal verificationTypeDal, IProductVerificationDal verificationDal,
        IProductVerificationVoteDal voteDal,
        IAuditLogDal auditLogDal, IUnitOfWork unitOfWork, IProductVerificationReadDal readDal)
    {
        _productDal = productDal;
        _verificationTypeDal = verificationTypeDal;
        _verificationDal = verificationDal;
        _voteDal = voteDal;
        _readDal = readDal;
        _auditLogDal = auditLogDal;
        _unitOfWork = unitOfWork;
    }

    public async Task<IDataResult<ProductVerificationSummaryDto>> GetSummaryAsync(int productId, int? currentUserId,
        CancellationToken cancellationToken = default)
    {
        var state = await _readDal.GetPublishedStateAsync(productId, currentUserId, cancellationToken);
        if (state is null)
            return new NotFoundDataResult<ProductVerificationSummaryDto>("Product was not found.");
        var positiveVotes = await _voteDal.CountCurrentlyEligiblePositiveVotesAsync(productId,
            DateTime.UtcNow.AddDays(-CommunityVerificationPolicy.MinimumAccountAgeDays), cancellationToken);

        return new SuccessDataResult<ProductVerificationSummaryDto>(new ProductVerificationSummaryDto(
            state.AdminVerified, state.CommunityVerified, state.CommunityRevoked,
            state.CommunityPendingReview, state.CommunityReviewRejected, positiveVotes,
            CommunityVerificationPolicy.PositiveVoteQuorum,
            currentUserId.HasValue ? state.CurrentUserVote.HasValue : null,
            state.CurrentUserVote));
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
