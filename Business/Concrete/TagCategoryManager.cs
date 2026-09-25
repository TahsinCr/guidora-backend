using Business.Abstract;
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
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Business.Concrete;

public sealed class TagCategoryManager : ITagCategoryService
{
    private readonly ITagCategoryDal _dal;
    private readonly ITagDal _tagDal;
    private readonly IProductTagDal _productTagDal;
    private readonly IAuditLogDal _audit;
    private readonly IUnitOfWork _unitOfWork;

    public TagCategoryManager(ITagCategoryDal dal, ITagDal tagDal, IProductTagDal productTagDal,
        IAuditLogDal audit, IUnitOfWork unitOfWork)
    {
        _dal = dal;
        _tagDal = tagDal;
        _productTagDal = productTagDal;
        _audit = audit;
        _unitOfWork = unitOfWork;
    }

    public async Task<IDataResult<List<TagCategory>>> GetActiveAsync(CancellationToken cancellationToken = default) =>
        new SuccessDataResult<List<TagCategory>>(await _dal.GetAllAsync(item => item.IsActive, cancellationToken));

    [SecuredOperationAspect("TagCategory.Manage")]
    [ValidationAspect(typeof(TagCategoryRequestValidator))]
    [TransactionScopeAspect]
    public async Task<IResult> CreateAsync(TagCategoryRequest request, int actorUserId,
        CancellationToken cancellationToken = default)
    {
        var slug = ProductIdentityNormalizer.NormalizeSlug(request.Slug);
        if (await _dal.AnyAsync(item => item.Slug == slug, cancellationToken))
            return new ConflictResult("A TagCategory with this slug already exists.");
        var entity = new TagCategory
        {
            Name = request.Name.Trim(), Slug = slug,
            AllowsMultipleSelection = request.AllowsMultipleSelection, IsActive = true
        };
        await _dal.AddAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _audit.AddAsync(AuditLogFactory.Create(actorUserId, nameof(TagCategory), entity.Id.ToString(), "Created",
            new { }, new { entity.Name, entity.Slug, entity.AllowsMultipleSelection, entity.IsActive }), cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return new SuccessResult("Tag category created.");
    }

    [SecuredOperationAspect("TagCategory.Manage")]
    [ValidationAspect(typeof(TagCategoryRequestValidator))]
    [TransactionScopeAspect]
    public async Task<IResult> UpdateAsync(int id, TagCategoryRequest request, int actorUserId,
        CancellationToken cancellationToken = default)
    {
        var entity = await _dal.GetAsync(item => item.Id == id, cancellationToken);
        if (entity is null) return new NotFoundResult("TagCategory was not found.");
        var slug = ProductIdentityNormalizer.NormalizeSlug(request.Slug);
        if (await _dal.AnyAsync(item => item.Id != id && item.Slug == slug, cancellationToken))
            return new ConflictResult("A TagCategory with this slug already exists.");
        if (entity.AllowsMultipleSelection != request.AllowsMultipleSelection)
        {
            if (await _productTagDal.HasProductsWithMultipleTagsInCategoryAsync(id, cancellationToken))
                return new ConflictResult("Selection mode cannot change while a Product has multiple Tags from this category.");
        }
        var oldValues = new { entity.Name, entity.Slug, entity.AllowsMultipleSelection, entity.IsActive };
        entity.Name = request.Name.Trim();
        entity.Slug = slug;
        entity.AllowsMultipleSelection = request.AllowsMultipleSelection;
        _dal.Update(entity);
        await _audit.AddAsync(AuditLogFactory.Create(actorUserId, nameof(TagCategory), id.ToString(), "Updated",
            oldValues, new { entity.Name, entity.Slug, entity.AllowsMultipleSelection, entity.IsActive }), cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return new SuccessResult("Tag category updated.");
    }

    [SecuredOperationAspect("TagCategory.Manage")]
    [TransactionScopeAspect]
    public async Task<IResult> DeactivateAsync(int id, int actorUserId, CancellationToken cancellationToken = default)
    {
        var entity = await _dal.GetAsync(item => item.Id == id, cancellationToken);
        if (entity is null) return new NotFoundResult("TagCategory was not found.");
        if (!entity.IsActive) return new SuccessResult("Tag category is already inactive.");
        entity.IsActive = false;
        _dal.Update(entity);
        var deactivatedTagCount = await _tagDal.DeactivateByCategoryAsync(id, cancellationToken);
        await _audit.AddAsync(AuditLogFactory.Create(actorUserId, nameof(TagCategory), id.ToString(), "Deactivated",
            new { IsActive = true }, new { IsActive = false, DeactivatedTagCount = deactivatedTagCount }),
            cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return new SuccessResult("Tag category deactivated.");
    }
}
