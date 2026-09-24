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

public sealed class ProductContributorManager : IProductContributorService
{
    private readonly IProductContributorDal _contributorDal;
    private readonly IProductDal _productDal;
    private readonly IUserDal _userDal;
    private readonly IProductResourceRules _rules;
    private readonly IAuditLogDal _auditLogDal;
    private readonly IUnitOfWork _unitOfWork;

    public ProductContributorManager(IProductContributorDal contributorDal, IProductDal productDal, IUserDal userDal,
        IProductResourceRules rules, IAuditLogDal auditLogDal, IUnitOfWork unitOfWork)
    {
        _contributorDal = contributorDal;
        _productDal = productDal;
        _userDal = userDal;
        _rules = rules;
        _auditLogDal = auditLogDal;
        _unitOfWork = unitOfWork;
    }

    public async Task<IDataResult<List<ProductContributor>>> GetByProductAsync(int productId,
        CancellationToken cancellationToken = default)
    {
        var rule = await _rules.EnsurePublishedAsync(productId, cancellationToken);
        if (!rule.Success) return new ErrorDataResult<List<ProductContributor>>(rule.Message);
        var contributors = await _contributorDal.GetAllAsync(item => item.ProductId == productId, cancellationToken);
        return new SuccessDataResult<List<ProductContributor>>(contributors.OrderBy(item => item.CreatedAt).ToList());
    }

    [SecuredOperationAspect("Product.Contributor.Manage")]
    [ValidationAspect(typeof(ProductContributorRequestValidator))]
    [TransactionScopeAspect]
    public async Task<IResult> AddAsync(int productId, int actorUserId, ProductContributorRequest request,
        CancellationToken cancellationToken = default)
    {
        var moderationResult = _rules.EnsureContentAllowed(request.ContributionType);
        if (!moderationResult.Success) return moderationResult;
        var rule = await _rules.EnsurePublishedAsync(productId, cancellationToken);
        if (!rule.Success) return rule;
        var product = await _productDal.GetAsync(item => item.ProductId == productId, cancellationToken);
        if (product is null) return new NotFoundResult("Product was not found.");
        if (actorUserId != request.UserId && actorUserId != product.CreatedByUserId)
            return new ForbiddenResult("Only the Product creator may credit another contributor.");
        var user = await _userDal.GetAsync(item => item.Id == request.UserId, cancellationToken);
        if (user is not { Status: true }) return new NotFoundResult("Contributor account is unavailable.");
        var existing = await _contributorDal.GetAsync(item => item.ProductId == productId && item.UserId == request.UserId,
            cancellationToken);
        if (existing is not null)
        {
            if (existing.ContributionType == request.ContributionType.Trim())
                return new SuccessResult("Contributor is already credited with this contribution.");
            var previous = existing.ContributionType;
            existing.ContributionType = request.ContributionType.Trim();
            _contributorDal.Update(existing);
            await _auditLogDal.AddAsync(AuditLogFactory.Create(actorUserId, nameof(ProductContributor),
                $"{productId}:{request.UserId}", "ContributionUpdated", new { ContributionType = previous },
                new { ContributionType = existing.ContributionType }), cancellationToken);
        }
        else
        {
            var contributor = new ProductContributor
            {
                ProductId = productId, UserId = request.UserId,
                ContributionType = request.ContributionType.Trim(), CreatedAt = System.DateTime.UtcNow
            };
            await _contributorDal.AddAsync(contributor, cancellationToken);
            await _auditLogDal.AddAsync(AuditLogFactory.Create(actorUserId, nameof(ProductContributor),
                $"{productId}:{request.UserId}", "Added", new { }, new
                {
                    contributor.ProductId, contributor.UserId, contributor.ContributionType, contributor.CreatedAt
                }), cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return new SuccessResult("Product contributor saved.");
    }

    [SecuredOperationAspect("Product.Contributor.Manage")]
    [TransactionScopeAspect]
    public async Task<IResult> RemoveAsync(int productId, int userId, int actorUserId,
        CancellationToken cancellationToken = default)
    {
        var rule = await _rules.EnsurePublishedAsync(productId, cancellationToken);
        if (!rule.Success) return rule;
        var product = await _productDal.GetAsync(item => item.ProductId == productId, cancellationToken);
        if (product is null) return new NotFoundResult("Product was not found.");
        if (actorUserId != userId && actorUserId != product.CreatedByUserId)
            return new ForbiddenResult("Only the contributor or Product creator may remove this credit.");
        var contributor = await _contributorDal.GetAsync(item => item.ProductId == productId && item.UserId == userId,
            cancellationToken);
        if (contributor is null) return new SuccessResult("Contributor was not listed.");
        _contributorDal.Purge(contributor);
        await _auditLogDal.AddAsync(AuditLogFactory.Create(actorUserId, nameof(ProductContributor),
            $"{productId}:{userId}", "Removed", new
            {
                contributor.ProductId, contributor.UserId, contributor.ContributionType, contributor.CreatedAt
            }, new { }), cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return new SuccessResult("Product contributor removed.");
    }
}
