using Business.Abstract;
using Core.Utilities.Results;
using DataAccess.Abstract;
using Entities;
using System.Collections.Generic;
using Core.Aspects.Caching;
using Core.Aspects.Performance;
using Core.CrossCuttingConcerns.Caching;
using Business.BusinessAspect.Autofac;
using Core.DataAccess;
using Business.BusinessRules.Catalog;
using Entities.DTOs;
using System;
using System.Threading;
using System.Threading.Tasks;
using Core.Aspects.Autofac.Validation;
using Business.ValidationRules.FluentValidation;
using Business.Utilities;
using Core.Aspects.Transaction;

namespace Business.Concrete
{
    public class CategoryManager : ICategoryService
    {
        private readonly ICategoryDal _categoryDal;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICategoryBusinessRules _businessRules;
        private readonly IAuditLogDal _auditLogDal;

        public CategoryManager(ICategoryDal categoryDal, IUnitOfWork unitOfWork,
            ICategoryBusinessRules businessRules, IAuditLogDal auditLogDal)
        {
            _categoryDal = categoryDal;
            _unitOfWork = unitOfWork;
            _businessRules = businessRules;
            _auditLogDal = auditLogDal;
        }

        [PerformanceAspect(1)]
        [CacheAspect(CacheProfiles.CatalogNavigation, duration: 60)]
        public async Task<IDataResult<List<Category>>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            return new SuccessDataResult<List<Category>>(
                await _categoryDal.GetAllAsync(category => category.IsActive, cancellationToken));
        }

        [SecuredOperationAspect("Category.Manage")]
        public async Task<IDataResult<List<Category>>> GetAllForManagementAsync(
            CancellationToken cancellationToken = default)
        {
            return new SuccessDataResult<List<Category>>(
                await _categoryDal.GetAllAsync(cancellationToken: cancellationToken));
        }

        [PerformanceAspect(1)]
        [CacheAspect(CacheProfiles.CatalogNavigation, duration: 60)]
        public async Task<IDataResult<Category>> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            var category = await _categoryDal.GetAsync(category => category.CategoryId == id && category.IsActive,
                cancellationToken);
            return category == null
                ? new NotFoundDataResult<Category>("Category was not found.")
                : new SuccessDataResult<Category>(category);
        }

        [SecuredOperationAspect("Category.Manage")]
        [ValidationAspect(typeof(CategoryRequestValidator))]
        [CacheRemoveAspect(CacheProfiles.CatalogNavigation)]
        [TransactionScopeAspect]
        public async Task<IResult> CreateAsync(CategoryRequest request, int createdByUserId,
            CancellationToken cancellationToken = default)
        {
            var ruleResult = await _businessRules.EnsureSlugIsAvailableAsync(request.Slug,
                cancellationToken: cancellationToken);
            if (!ruleResult.Success) return ruleResult;

            var category = new Category
            {
                CategoryName = request.CategoryName.Trim(),
                Slug = request.Slug.Trim(),
                Description = request.Description.Trim(),
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            await _categoryDal.AddAsync(category, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _auditLogDal.AddAsync(AuditLogFactory.Create(createdByUserId, nameof(Category),
                category.CategoryId.ToString(), "Created", new { }, new
                {
                    category.CategoryName, category.Slug, category.Description, category.IsActive,
                    category.CreatedAt
                }), cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return new SuccessResult();
        }

        [SecuredOperationAspect("Category.Manage")]
        [ValidationAspect(typeof(CategoryRequestValidator))]
        [CacheRemoveAspect(CacheProfiles.CatalogNavigation)]
        [TransactionScopeAspect]
        public async Task<IResult> UpdateAsync(int categoryId, CategoryRequest request, int updatedByUserId,
            CancellationToken cancellationToken = default)
        {
            var category = await _categoryDal.GetAsync(item => item.CategoryId == categoryId, cancellationToken);
            if (category is null) return new NotFoundResult("Category was not found.");

            var ruleResult = await _businessRules.EnsureSlugIsAvailableAsync(request.Slug, categoryId,
                cancellationToken);
            if (!ruleResult.Success) return ruleResult;

            var oldValues = new
            {
                category.CategoryName, category.Slug, category.Description, category.IsActive, category.UpdatedAt
            };
            category.CategoryName = request.CategoryName.Trim();
            category.Slug = request.Slug.Trim();
            category.Description = request.Description.Trim();
            category.UpdatedAt = DateTime.UtcNow;
            _categoryDal.Update(category);
            await _auditLogDal.AddAsync(AuditLogFactory.Create(updatedByUserId, nameof(Category), categoryId.ToString(),
                "Updated", oldValues, new
                {
                    category.CategoryName, category.Slug, category.Description, category.IsActive,
                    category.UpdatedAt
                }), cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return new SuccessResult();
        }

        [SecuredOperationAspect("Category.Manage")]
        [CacheRemoveAspect(CacheProfiles.CatalogNavigation, CacheProfiles.CatalogListing)]
        [TransactionScopeAspect]
        public async Task<IResult> DeactivateAsync(int categoryId, int updatedByUserId,
            CancellationToken cancellationToken = default)
        {
            var category = await _categoryDal.GetAsync(item => item.CategoryId == categoryId, cancellationToken);
            if (category is null) return new NotFoundResult("Category was not found.");

            var oldValues = new { category.IsActive, category.UpdatedAt };
            category.IsActive = false;
            category.UpdatedAt = DateTime.UtcNow;
            _categoryDal.Update(category);
            await _auditLogDal.AddAsync(AuditLogFactory.Create(updatedByUserId, nameof(Category), categoryId.ToString(),
                "Deactivated", oldValues, new { category.IsActive, category.UpdatedAt }), cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return new SuccessResult();
        }
    }
}
