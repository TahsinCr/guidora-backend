using Business.Abstract;
using Business.Constants;
using Business.BusinessAspect.Autofac;
using Business.BusinessRules.Content;
using Business.Utilities;
using Business.ValidationRules.FluentValidation;
using Core.Aspects.Autofac.Validation;
using Core.Aspects.Transaction;
using Core.DataAccess;
using Core.Utilities.Results;
using DataAccess.Abstract;
using Entities;
using Entities.DTOs;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Business.Concrete;

public sealed class ProductMediaManager : IProductMediaService
{
    private readonly IProductMediaDal _mediaDal;
    private readonly IProductResourceRules _rules;
    private readonly IAuditLogDal _auditLogDal;
    private readonly IUnitOfWork _unitOfWork;

    public ProductMediaManager(IProductMediaDal mediaDal, IProductResourceRules rules,
        IAuditLogDal auditLogDal, IUnitOfWork unitOfWork)
    {
        _mediaDal = mediaDal;
        _rules = rules;
        _auditLogDal = auditLogDal;
        _unitOfWork = unitOfWork;
    }

    public async Task<IDataResult<List<ProductMedia>>> GetByProductAsync(int productId,
        CancellationToken cancellationToken = default)
    {
        var rule = await _rules.EnsurePublishedAsync(productId, cancellationToken);
        if (!rule.Success) return new ErrorDataResult<List<ProductMedia>>(rule.Message);
        var media = await _mediaDal.GetAllAsync(item => item.ProductId == productId, cancellationToken);
        return new SuccessDataResult<List<ProductMedia>>(media.OrderBy(item => item.DisplayOrder).ThenBy(item => item.Id).ToList());
    }

    [SecuredOperationAspect("Product.Content.Manage")]
    [ValidationAspect(typeof(ProductMediaRequestValidator))]
    [TransactionScopeAspect]
    public async Task<IResult> CreateAsync(int productId, int actorUserId, ProductMediaRequest request,
        CancellationToken cancellationToken = default)
    {
        var moderationResult = _rules.EnsureContentAllowed(request.AltText);
        if (!moderationResult.Success) return moderationResult;
        var rule = await _rules.EnsureDirectContentChangeAllowedAsync(productId, cancellationToken);
        if (!rule.Success) return rule;
        var media = new ProductMedia
        {
            ProductId = productId, MediaType = request.MediaType.Trim(), Url = request.Url.Trim(),
            AltText = request.AltText.Trim(), DisplayOrder = request.DisplayOrder, CreatedAt = System.DateTime.UtcNow
        };
        await _mediaDal.AddAsync(media, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _auditLogDal.AddAsync(AuditLogFactory.Create(actorUserId, nameof(ProductMedia), media.Id.ToString(),
            "Created", new { }, new { media.ProductId, media.MediaType, media.Url, media.AltText,
                media.DisplayOrder, media.CreatedAt }), cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return new SuccessResult("Product media metadata created.");
    }

    [SecuredOperationAspect("Product.Content.Manage")]
    [ValidationAspect(typeof(ProductMediaRequestValidator))]
    [TransactionScopeAspect]
    public async Task<IResult> UpdateAsync(int productId, int mediaId, int actorUserId, ProductMediaRequest request,
        CancellationToken cancellationToken = default)
    {
        var moderationResult = _rules.EnsureContentAllowed(request.AltText);
        if (!moderationResult.Success) return moderationResult;
        var rule = await _rules.EnsureDirectContentChangeAllowedAsync(productId, cancellationToken);
        if (!rule.Success) return rule;
        var media = await _mediaDal.GetAsync(item => item.ProductId == productId && item.Id == mediaId,
            cancellationToken);
        if (media is null) return new NotFoundResult("Product media was not found.");
        var oldValues = new { media.MediaType, media.Url, media.AltText, media.DisplayOrder };
        media.MediaType = request.MediaType.Trim();
        media.Url = request.Url.Trim();
        media.AltText = request.AltText.Trim();
        media.DisplayOrder = request.DisplayOrder;
        _mediaDal.Update(media);
        await _auditLogDal.AddAsync(AuditLogFactory.Create(actorUserId, nameof(ProductMedia), mediaId.ToString(),
            "Updated", oldValues, new { media.MediaType, media.Url, media.AltText, media.DisplayOrder }), cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return new SuccessResult("Product media metadata updated.");
    }

    [SecuredOperationAspect("Product.Content.Manage")]
    [TransactionScopeAspect]
    public async Task<IResult> DeleteAsync(int productId, int mediaId, int actorUserId,
        CancellationToken cancellationToken = default)
    {
        var rule = await _rules.EnsureDirectContentChangeAllowedAsync(productId, cancellationToken);
        if (!rule.Success) return rule;
        var media = await _mediaDal.GetAsync(item => item.ProductId == productId && item.Id == mediaId,
            cancellationToken);
        if (media is null) return new NotFoundResult("Product media was not found.");
        _mediaDal.Purge(media);
        await _auditLogDal.AddAsync(AuditLogFactory.Create(actorUserId, nameof(ProductMedia), mediaId.ToString(),
            "Deleted", new { media.ProductId, media.MediaType, media.Url, media.AltText, media.DisplayOrder }, new { }),
            cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return new SuccessResult("Product media metadata deleted.");
    }
}
