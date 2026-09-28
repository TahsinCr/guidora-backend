using Business.Abstract;
using Business.BusinessAspect.Autofac;
using Business.Utilities;
using Business.ValidationRules.FluentValidation;
using Business.Constants;
using Core.Aspects.Caching;
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

public sealed class TagManager : ITagService
{
    private readonly ITagDal _dal;
    private readonly ITagCategoryDal _categoryDal;
    private readonly IProductTagDal _productTagDal;
    private readonly IAuditLogDal _audit;
    private readonly IUnitOfWork _unitOfWork;

    public TagManager(ITagDal dal, ITagCategoryDal categoryDal, IProductTagDal productTagDal,
        IAuditLogDal audit, IUnitOfWork unitOfWork)
    {
        _dal = dal;
        _categoryDal = categoryDal;
        _productTagDal = productTagDal;
        _audit = audit;
        _unitOfWork = unitOfWork;
    }

    [CacheAspect(CacheProfiles.CatalogNavigation, duration: 60)]
    public async Task<IDataResult<List<Tag>>> GetActiveAsync(int? tagCategoryId = null,
        CancellationToken cancellationToken = default)
    {
        var activeCategories = await _categoryDal.GetAllAsync(item => item.IsActive &&
            (!tagCategoryId.HasValue || item.Id == tagCategoryId.Value), cancellationToken);
        var ids = activeCategories.Select(item => item.Id).ToArray();
        var tags = await _dal.GetAllAsync(item => item.IsActive && ids.Contains(item.TagCategoryId), cancellationToken);
        return new SuccessDataResult<List<Tag>>(tags.OrderBy(item => item.TagCategoryId).ThenBy(item => item.Name).ToList());
    }

    [SecuredOperationAspect("Tag.Manage")]
    [ValidationAspect(typeof(TagRequestValidator))]
    [CacheRemoveAspect(CacheProfiles.CatalogNavigation)]
    [TransactionScopeAspect]
    public async Task<IResult> CreateAsync(TagRequest request, int actorUserId,
        CancellationToken cancellationToken = default)
    {
        var category = await _categoryDal.GetAsync(item => item.Id == request.TagCategoryId && item.IsActive,
            cancellationToken);
        if (category is null) return new NotFoundResult("Active TagCategory was not found.");
        var slug = ProductIdentityNormalizer.NormalizeSlug(request.Slug);
        if (await _dal.AnyAsync(item => item.TagCategoryId == category.Id && item.Slug == slug, cancellationToken))
            return new ConflictResult("A Tag with this slug already exists in the category.");
        var entity = new Tag
        {
            TagCategoryId = category.Id, Name = request.Name.Trim(), Slug = slug,
            IsExclusive = request.IsExclusive, IsActive = true
        };
        await _dal.AddAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _audit.AddAsync(AuditLogFactory.Create(actorUserId, nameof(Tag), entity.Id.ToString(), "Created",
            new { }, new { entity.TagCategoryId, entity.Name, entity.Slug, entity.IsExclusive, entity.IsActive }), cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return new SuccessResult("Tag created.");
    }

    [SecuredOperationAspect("Tag.Manage")]
    [ValidationAspect(typeof(TagRequestValidator))]
    [CacheRemoveAspect(CacheProfiles.CatalogNavigation)]
    [TransactionScopeAspect]
    public async Task<IResult> UpdateAsync(int id, TagRequest request, int actorUserId,
        CancellationToken cancellationToken = default)
    {
        var entity = await _dal.GetAsync(item => item.Id == id, cancellationToken);
        if (entity is null) return new NotFoundResult("Tag was not found.");
        if (entity.TagCategoryId != request.TagCategoryId &&
            await _productTagDal.AnyAsync(item => item.TagId == id, cancellationToken))
            return new ConflictResult("A Tag assigned to Products cannot be moved to another TagCategory.");
        if (!entity.IsExclusive && request.IsExclusive &&
            await _productTagDal.HasOtherTagInSameCategoryForAssignedProductsAsync(
                id, entity.TagCategoryId, cancellationToken))
            return new ConflictResult("A Product-assigned Tag cannot become exclusive because existing selections may become invalid.");
        if (!await _categoryDal.AnyAsync(item => item.Id == request.TagCategoryId && item.IsActive, cancellationToken))
            return new NotFoundResult("Active TagCategory was not found.");
        var slug = ProductIdentityNormalizer.NormalizeSlug(request.Slug);
        if (await _dal.AnyAsync(item => item.Id != id && item.TagCategoryId == request.TagCategoryId &&
                                        item.Slug == slug, cancellationToken))
            return new ConflictResult("A Tag with this slug already exists in the category.");
        var oldValues = new { entity.TagCategoryId, entity.Name, entity.Slug, entity.IsExclusive, entity.IsActive };
        entity.TagCategoryId = request.TagCategoryId;
        entity.Name = request.Name.Trim();
        entity.Slug = slug;
        entity.IsExclusive = request.IsExclusive;
        _dal.Update(entity);
        await _audit.AddAsync(AuditLogFactory.Create(actorUserId, nameof(Tag), id.ToString(), "Updated", oldValues,
            new { entity.TagCategoryId, entity.Name, entity.Slug, entity.IsExclusive, entity.IsActive }), cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return new SuccessResult("Tag updated.");
    }

    [SecuredOperationAspect("Tag.Manage")]
    [CacheRemoveAspect(CacheProfiles.CatalogNavigation)]
    [TransactionScopeAspect]
    public async Task<IResult> DeactivateAsync(int id, int actorUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _dal.GetAsync(item => item.Id == id, cancellationToken);
        if (entity is null) return new NotFoundResult("Tag was not found.");
        if (!entity.IsActive) return new SuccessResult("Tag is already inactive.");
        entity.IsActive = false;
        _dal.Update(entity);
        await _audit.AddAsync(AuditLogFactory.Create(actorUserId, nameof(Tag), id.ToString(), "Deactivated",
            new { IsActive = true }, new { IsActive = false }), cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return new SuccessResult("Tag deactivated.");
    }
}
