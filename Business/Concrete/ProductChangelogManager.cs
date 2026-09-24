using Business.Abstract;
using Business.Constants;
using Business.BusinessAspect.Autofac;
using Business.BusinessRules.Content;
using Business.Utilities;
using Business.ValidationRules.FluentValidation;
using Core.Aspects.Autofac.Validation;
using Core.Aspects.Caching;
using Core.Aspects.Transaction;
using Core.DataAccess;
using Core.CrossCuttingConcerns.Caching;
using Core.Utilities.Results;
using DataAccess.Abstract;
using Entities;
using Entities.DTOs;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

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

    public async Task<IDataResult<List<ProductChangelog>>> GetByProductAsync(int productId,
        CancellationToken cancellationToken = default)
    {
        var rule = await _rules.EnsurePublishedAsync(productId, cancellationToken);
        if (!rule.Success) return new ErrorDataResult<List<ProductChangelog>>(rule.Message);
        var entries = await _changelogDal.GetAllAsync(item => item.ProductId == productId, cancellationToken);
        return new SuccessDataResult<List<ProductChangelog>>(entries.OrderByDescending(item => item.ReleaseDate)
            .ThenByDescending(item => item.Id).ToList());
    }

    [SecuredOperationAspect("Product.Content.Manage")]
    [ValidationAspect(typeof(ProductChangelogRequestValidator))]
    [CacheRemoveAspect(CacheProfiles.ProductDetail)]
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
            return new ErrorResult("This Product version already has a changelog entry.");

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
