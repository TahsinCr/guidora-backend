using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Business.Abstract;
using Business.BusinessAspect.Autofac;
using Business.Constants;
using Business.ValidationRules.FluentValidation;
using Core.Aspects.Autofac.Validation;
using Core.Aspects.Caching;
using Core.CrossCuttingConcerns.Caching;
using Core.Business;
using Core.Entities;
using Core.Utilities.Results;
using DataAccess.Abstract;
using Entities;
using Entities.DTOs;
using Core.Aspects.Transaction;

namespace Business.Concrete
{
    public class ProductManager : IProductService
    {
        private readonly IProductDal _productDal;
        private readonly IProductCategoryDal _productCategoryDal;
        private readonly IProductStatusDal _productStatusDal;
        private readonly ICategoryDal _categoryDal;

        public ProductManager(IProductDal productDal, IProductCategoryDal productCategoryDal,
            IProductStatusDal productStatusDal, ICategoryDal categoryDal)
        {
            _productDal = productDal;
            _productCategoryDal = productCategoryDal;
            _productStatusDal = productStatusDal;
            _categoryDal = categoryDal;
        }

        [CacheAspect(CacheProfiles.CatalogListing, duration: 10)]
        public IDataResult<List<Product>> GetAll()
        {
            return new SuccessDataResult<List<Product>>(_productDal.GetAll(), Messages.ProductsListed);
        }

        [CacheAspect(CacheProfiles.CatalogListing, duration: 10)]
        public IDataResult<List<Product>> GetAllByCategoryId(int id)
        {
            var productIds = _productCategoryDal.GetAll(item => item.CategoryId == id)
                .Select(item => item.ProductId)
                .ToHashSet();
            return new SuccessDataResult<List<Product>>(
                _productDal.GetAll(product => productIds.Contains(product.ProductId)));
        }


        [CacheAspect(CacheProfiles.ProductDetail, duration: 15)]
        public IDataResult<Product> GetById(int id)
        {
            return new SuccessDataResult<Product>(_productDal.Get(product=> product.ProductId == id));
        }

        [SecuredOperationAspect("Product.Create")]
        [ValidationAspect(typeof(ProductValidator))]
        [CacheRemoveAspect(CacheProfiles.CatalogListing, CacheProfiles.ProductDetail)]
        public IResult Add(Product product)
        {
            IResult? result = CheckIfProductNameAlreadyExists(product.Name);
            if (result != null) return result;

            var publishedStatus = _productStatusDal.Get(status => status.Code == "published");
            if (publishedStatus == null)
            {
                return new ErrorResult("Published product status is not configured.");
            }

            product.ProductStatusId = publishedStatus.Id;
            product.CreatedAt = DateTime.UtcNow;
            _productDal.Add(product);
            return new SuccessResult(Messages.ProductAdded);
            
        }

        [TransactionScopeAspect]
        [SecuredOperationAspect("Product.Create")]
        [ValidationAspect(typeof(CreateProductRequestValidator))]
        [CacheRemoveAspect(CacheProfiles.CatalogListing, CacheProfiles.ProductDetail)]
        public IResult Create(CreateProductRequest request, int createdByUserId)
        {
            var product = new Product
            {
                Name = request.Name,
                Slug = request.Slug,
                ShortDescription = request.ShortDescription,
                Description = request.Description,
                WebsiteUrl = request.WebsiteUrl,
                LogoUrl = request.LogoUrl,
                CreatedByUserId = createdByUserId
            };

            var result = Add(product);
            if (!result.Success)
            {
                return result;
            }

            return ReplaceCategories(product.ProductId, request.CategoryIds);
        }

        [TransactionScopeAspect]
        [SecuredOperationAspect("Product.Update")]
        [ValidationAspect(typeof(UpdateProductRequestValidator))]
        [CacheRemoveAspect(CacheProfiles.CatalogListing, CacheProfiles.ProductDetail)]
        public IResult Update(int productId, UpdateProductRequest request)
        {
            var product = _productDal.Get(item => item.ProductId == productId);
            if (product == null)
            {
                return new ErrorResult("Product was not found.");
            }

            product.Name = request.Name;
            product.Slug = request.Slug;
            product.ShortDescription = request.ShortDescription;
            product.Description = request.Description;
            product.WebsiteUrl = request.WebsiteUrl;
            product.LogoUrl = request.LogoUrl;
            product.UpdatedAt = DateTime.UtcNow;

            var duplicateResult = CheckIfProductNameAlreadyExists(product.Name, product.ProductId);
            if (!duplicateResult.Success)
            {
                return duplicateResult;
            }

            _productDal.Update(product);
            return ReplaceCategories(product.ProductId, request.CategoryIds);
        }

        [TransactionScopeAspect]
        [SecuredOperationAspect("Product.Delete")]
        [CacheRemoveAspect(CacheProfiles.CatalogListing, CacheProfiles.ProductDetail)]
        public IResult Delete(int productId)
        {
            var product = _productDal.Get(item => item.ProductId == productId);
            if (product == null)
            {
                return new ErrorResult("Product was not found.");
            }

            var deletedStatus = _productStatusDal.Get(status => status.Code == "deleted");
            if (deletedStatus == null)
            {
                return new ErrorResult("Deleted product status is not configured.");
            }

            product.ProductStatusId = deletedStatus.Id;
            product.UpdatedAt = DateTime.UtcNow;
            _productDal.Update(product);
            return new SuccessResult();
        }

        [SecuredOperationAspect("Product.Update")]
        [ValidationAspect(typeof(ProductValidator))]
        [CacheRemoveAspect(CacheProfiles.CatalogListing, CacheProfiles.ProductDetail)]
        public IResult Update(Product entity)
        {
            _productDal.Update(entity);
            return new SuccessResult();
        }

        [SecuredOperationAspect("Product.Delete")]
        [CacheRemoveAspect(CacheProfiles.CatalogListing, CacheProfiles.ProductDetail)]
        public IResult Delete(Product entity)
        {
            _productDal.Delete(entity);
            return new SuccessResult();
        }

        private IResult CheckIfProductNameAlreadyExists(string productName, int? excludingProductId = null)
        {
            bool result = _productDal.GetAll(p => p.Name == productName &&
                (!excludingProductId.HasValue || p.ProductId != excludingProductId.Value)).Any();
            if (result)
            {
                return new ErrorResult(Messages.ProductNameAlreadyExistsError);
            }

            return new SuccessResult();
        }

        private IResult ReplaceCategories(int productId, IEnumerable<int> categoryIds)
        {
            var normalizedCategoryIds = categoryIds.Distinct().ToList();
            if (normalizedCategoryIds.Count == 0)
            {
                return new ErrorResult("At least one category is required.");
            }

            var knownCategoryIds = _categoryDal.GetAll(category =>
                normalizedCategoryIds.Contains(category.CategoryId))
                .Select(category => category.CategoryId)
                .ToHashSet();
            if (knownCategoryIds.Count != normalizedCategoryIds.Count)
            {
                return new ErrorResult("One or more categories do not exist.");
            }

            foreach (var existing in _productCategoryDal.GetAll(item => item.ProductId == productId))
            {
                _productCategoryDal.Delete(existing);
            }

            foreach (var categoryId in normalizedCategoryIds)
            {
                _productCategoryDal.Add(new ProductCategory { ProductId = productId, CategoryId = categoryId });
            }

            return new SuccessResult();
        }
    }
}
