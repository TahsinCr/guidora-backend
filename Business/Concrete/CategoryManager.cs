using Business.Abstract;
using Core.Utilities.Results;
using DataAccess.Abstract;
using Entities;
using System.Collections.Generic;
using Core.Aspects.Caching;
using Core.CrossCuttingConcerns.Caching;
using Business.BusinessAspect.Autofac;
using Core.DataAccess;
using Business.BusinessRules.Catalog;
using Entities.DTOs;
using System;
using Core.Aspects.Autofac.Validation;
using Business.ValidationRules.FluentValidation;

namespace Business.Concrete
{
    public class CategoryManager : ICategoryService
    {
        private readonly ICategoryDal _categoryDal;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICategoryBusinessRules _businessRules;

        public CategoryManager(ICategoryDal categoryDal, IUnitOfWork unitOfWork,
            ICategoryBusinessRules businessRules)
        {
            _categoryDal = categoryDal;
            _unitOfWork = unitOfWork;
            _businessRules = businessRules;
        }

        [CacheAspect(CacheProfiles.CatalogNavigation, duration: 60)]
        public IDataResult<List<Category>> GetAll()
        {
            return new SuccessDataResult<List<Category>>(_categoryDal.GetAll());
        }

        [CacheAspect(CacheProfiles.CatalogNavigation, duration: 60)]
        public IDataResult<Category> GetById(int id)
        {
            var category = _categoryDal.Get(category => category.CategoryId == id);
            return category == null
                ? new ErrorDataResult<Category>("Category was not found.")
                : new SuccessDataResult<Category>(category);
        }

        [CacheRemoveAspect(CacheProfiles.CatalogNavigation)]
        [SecuredOperationAspect("Category.Manage")]
        [ValidationAspect(typeof(CategoryRequestValidator))]
        public IResult Create(CategoryRequest request)
        {
            var ruleResult = _businessRules.EnsureSlugIsAvailable(request.Slug);
            if (!ruleResult.Success) return ruleResult;

            var category = new Category
            {
                CategoryName = request.CategoryName.Trim(),
                Slug = request.Slug.Trim(),
                Description = request.Description.Trim(),
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            _categoryDal.Add(category);
            _unitOfWork.SaveChanges();
            return new SuccessResult();
        }

        [CacheRemoveAspect(CacheProfiles.CatalogNavigation)]
        [SecuredOperationAspect("Category.Manage")]
        [ValidationAspect(typeof(CategoryRequestValidator))]
        public IResult Update(int categoryId, CategoryRequest request)
        {
            var category = _categoryDal.Get(item => item.CategoryId == categoryId);
            if (category is null) return new ErrorResult("Category was not found.");

            var ruleResult = _businessRules.EnsureSlugIsAvailable(request.Slug, categoryId);
            if (!ruleResult.Success) return ruleResult;

            category.CategoryName = request.CategoryName.Trim();
            category.Slug = request.Slug.Trim();
            category.Description = request.Description.Trim();
            category.UpdatedAt = DateTime.UtcNow;
            _categoryDal.Update(category);
            _unitOfWork.SaveChanges();
            return new SuccessResult();
        }

        [CacheRemoveAspect(CacheProfiles.CatalogNavigation)]
        [SecuredOperationAspect("Category.Manage")]
        public IResult Deactivate(int categoryId)
        {
            var category = _categoryDal.Get(item => item.CategoryId == categoryId);
            if (category is null) return new ErrorResult("Category was not found.");

            category.IsActive = false;
            category.UpdatedAt = DateTime.UtcNow;
            _categoryDal.Update(category);
            _unitOfWork.SaveChanges();
            return new SuccessResult();
        }
    }
}
