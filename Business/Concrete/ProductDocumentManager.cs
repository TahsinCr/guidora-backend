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
using System.Threading;
using System.Threading.Tasks;
using Core.Pagination;

namespace Business.Concrete;

public sealed class ProductDocumentManager : IProductDocumentService
{
    private readonly IProductDocumentDal _documentDal;
    private readonly IProductResourceRules _rules;
    private readonly IAuditLogDal _auditLogDal;
    private readonly IUnitOfWork _unitOfWork;

    public ProductDocumentManager(IProductDocumentDal documentDal, IProductResourceRules rules,
        IAuditLogDal auditLogDal, IUnitOfWork unitOfWork)
    {
        _documentDal = documentDal;
        _rules = rules;
        _auditLogDal = auditLogDal;
        _unitOfWork = unitOfWork;
    }

    public async Task<IDataResult<PagedResult<ProductDocumentListItemDto>>> GetByProductAsync(int productId,
        PageRequest page,
        CancellationToken cancellationToken = default)
    {
        if (page is null || page.Page < 1 || page.PageSize is < 1 or > 100 ||
            ((long)page.Page - 1) * page.PageSize > int.MaxValue)
            return new ErrorDataResult<PagedResult<ProductDocumentListItemDto>>("Invalid document page request.");
        var rule = await _rules.EnsurePublishedAsync(productId, cancellationToken);
        if (!rule.Success) return new NotFoundDataResult<PagedResult<ProductDocumentListItemDto>>("Product was not found.");
        return new SuccessDataResult<PagedResult<ProductDocumentListItemDto>>(
            await _documentDal.GetPageAsync(productId, page.Page, page.PageSize, cancellationToken));
    }

    public async Task<IDataResult<ProductDocument>> GetByIdAsync(int productId, int documentId,
        CancellationToken cancellationToken = default)
    {
        var rule = await _rules.EnsurePublishedAsync(productId, cancellationToken);
        if (!rule.Success) return new NotFoundDataResult<ProductDocument>("Product was not found.");
        var document = await _documentDal.GetAsync(item => item.ProductId == productId && item.Id == documentId,
            cancellationToken);
        return document is null ? new NotFoundDataResult<ProductDocument>("Product document was not found.")
            : new SuccessDataResult<ProductDocument>(document);
    }

    [SecuredOperationAspect("Product.Content.Manage")]
    [ValidationAspect(typeof(ProductDocumentRequestValidator))]
    [TransactionScopeAspect]
    public async Task<IResult> CreateAsync(int productId, int actorUserId, ProductDocumentRequest request,
        CancellationToken cancellationToken = default)
    {
        var moderationResult = _rules.EnsureContentAllowed(request.Title, request.Content);
        if (!moderationResult.Success) return moderationResult;
        var rule = await _rules.EnsureDirectContentChangeAllowedAsync(productId, cancellationToken);
        if (!rule.Success) return rule;
        var document = new ProductDocument
        {
            ProductId = productId, DocumentType = request.DocumentType.Trim(), Title = request.Title.Trim(),
            Content = request.Content, CreatedAt = System.DateTime.UtcNow
        };
        await _documentDal.AddAsync(document, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _auditLogDal.AddAsync(AuditLogFactory.Create(actorUserId, nameof(ProductDocument), document.Id.ToString(),
            "Created", new { }, new { document.ProductId, document.DocumentType, document.Title, document.Content,
                document.CreatedAt }), cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return new SuccessResult("Product document created.");
    }

    [SecuredOperationAspect("Product.Content.Manage")]
    [ValidationAspect(typeof(ProductDocumentRequestValidator))]
    [TransactionScopeAspect]
    public async Task<IResult> UpdateAsync(int productId, int documentId, int actorUserId,
        ProductDocumentRequest request, CancellationToken cancellationToken = default)
    {
        var moderationResult = _rules.EnsureContentAllowed(request.Title, request.Content);
        if (!moderationResult.Success) return moderationResult;
        var rule = await _rules.EnsureDirectContentChangeAllowedAsync(productId, cancellationToken);
        if (!rule.Success) return rule;
        var document = await _documentDal.GetAsync(item => item.Id == documentId && item.ProductId == productId,
            cancellationToken);
        if (document is null) return new NotFoundResult("Product document was not found.");
        var oldValues = new { document.DocumentType, document.Title, document.Content, document.UpdatedAt };
        document.DocumentType = request.DocumentType.Trim();
        document.Title = request.Title.Trim();
        document.Content = request.Content;
        document.UpdatedAt = System.DateTime.UtcNow;
        _documentDal.Update(document);
        await _auditLogDal.AddAsync(AuditLogFactory.Create(actorUserId, nameof(ProductDocument), documentId.ToString(),
            "Updated", oldValues, new { document.DocumentType, document.Title, document.Content, document.UpdatedAt }),
            cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return new SuccessResult("Product document updated.");
    }

    [SecuredOperationAspect("Product.Content.Manage")]
    [TransactionScopeAspect]
    public async Task<IResult> DeleteAsync(int productId, int documentId, int actorUserId,
        CancellationToken cancellationToken = default)
    {
        var rule = await _rules.EnsureDirectContentChangeAllowedAsync(productId, cancellationToken);
        if (!rule.Success) return rule;
        var document = await _documentDal.GetAsync(item => item.Id == documentId && item.ProductId == productId,
            cancellationToken);
        if (document is null) return new NotFoundResult("Product document was not found.");
        _documentDal.Purge(document);
        await _auditLogDal.AddAsync(AuditLogFactory.Create(actorUserId, nameof(ProductDocument), documentId.ToString(),
            "Deleted", new { document.ProductId, document.DocumentType, document.Title, document.Content }, new { }),
            cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return new SuccessResult("Product document deleted.");
    }
}
