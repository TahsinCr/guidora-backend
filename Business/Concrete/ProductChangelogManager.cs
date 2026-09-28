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

public sealed class ProductChangelogManager : IProductChangelogService
{
    private readonly IProductChangelogDal _changelogDal;
    private readonly IProductResourceRules _rules;
    private readonly IAuditLogDal _auditLogDal;
    private readonly IUnitOfWork _unitOfWork;

    public ProductChangelogManager(IProductChangelogDal changelogDal, IProductResourceRules rules,
        IAuditLogDal auditLogDal, IUnitOfWork unitOfWork)
    {
        _changelogDal = changelogDal;
        _rules = rules;
        _auditLogDal = auditLogDal;
        _unitOfWork = unitOfWork;
    }

    public async Task<IDataResult<PagedResult<ProductChangelogListItemDto>>> GetByProductAsync(int productId,
        PageRequest page,
        CancellationToken cancellationToken = default)
    {
        if (page is null || page.Page < 1 || page.PageSize is < 1 or > 100 ||
            ((long)page.Page - 1) * page.PageSize > int.MaxValue)
            return new ErrorDataResult<PagedResult<ProductChangelogListItemDto>>("Invalid changelog page request.");
        var rule = await _rules.EnsurePublishedAsync(productId, cancellationToken);
        if (!rule.Success) return new NotFoundDataResult<PagedResult<ProductChangelogListItemDto>>("Product was not found.");
        return new SuccessDataResult<PagedResult<ProductChangelogListItemDto>>(
            await _changelogDal.GetPageAsync(productId, page.Page, page.PageSize, cancellationToken));
    }

    public async Task<IDataResult<ProductChangelog>> GetByIdAsync(int productId, int changelogId,
        CancellationToken cancellationToken = default)
    {
        var rule = await _rules.EnsurePublishedAsync(productId, cancellationToken);
        if (!rule.Success) return new NotFoundDataResult<ProductChangelog>("Product was not found.");
        var entry = await _changelogDal.GetAsync(item => item.ProductId == productId && item.Id == changelogId,
            cancellationToken);
        return entry is null ? new NotFoundDataResult<ProductChangelog>("Product changelog was not found.")
            : new SuccessDataResult<ProductChangelog>(entry);
    }

    [SecuredOperationAspect("Product.Content.Manage")]
    [ValidationAspect(typeof(ProductChangelogRequestValidator))]
    [TransactionScopeAspect]
    public async Task<IResult> CreateAsync(int productId, int actorUserId, ProductChangelogRequest request,
        CancellationToken cancellationToken = default)
    {
        var moderationResult = _rules.EnsureContentAllowed(request.Title, request.Description, request.Content);
        if (!moderationResult.Success) return moderationResult;
        var rule = await _rules.EnsurePublishedAsync(productId, cancellationToken);
        if (!rule.Success) return rule;
        if (await _changelogDal.AnyAsync(item => item.ProductId == productId && item.Version == request.Version,
                cancellationToken))
            return new ConflictResult("This Product version already has a changelog entry.");

        var entry = new ProductChangelog
        {
            ProductId = productId, Version = request.Version.Trim(), Title = request.Title.Trim(),
            Description = request.Description.Trim(), Content = request.Content,
            ReleaseDate = request.ReleaseDate.Kind == System.DateTimeKind.Utc
                ? request.ReleaseDate : request.ReleaseDate.ToUniversalTime(),
            CreatedAt = System.DateTime.UtcNow
        };
        await _changelogDal.AddAsync(entry, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _auditLogDal.AddAsync(AuditLogFactory.Create(actorUserId, nameof(ProductChangelog), entry.Id.ToString(),
            "Published", new { }, new { entry.ProductId, entry.Version, entry.Title, entry.Description,
                entry.Content, entry.ReleaseDate, entry.CreatedAt }), cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return new SuccessResult("Product changelog entry published.");
    }
}
