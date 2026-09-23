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
using Core.Entities;
using Core.Utilities.Results;
using DataAccess.Abstract;
using Entities;
using Entities.DTOs;
using Core.Aspects.Transaction;
using Core.DataAccess;
using Business.BusinessRules.Catalog;

namespace Business.Concrete
{
    public class ProductManager : IProductService
    {
        private readonly IProductDal _productDal;
        private readonly IProductCategoryDal _productCategoryDal;
        private readonly IProductStatusDal _productStatusDal;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IProductBusinessRules _businessRules;

        public ProductManager(IProductDal productDal, IProductCategoryDal productCategoryDal,
            IProductStatusDal productStatusDal, IUnitOfWork unitOfWork,
            IProductBusinessRules businessRules)
        {
            _productDal = productDal;
            _productCategoryDal = productCategoryDal;
            _productStatusDal = productStatusDal;
            _unitOfWork = unitOfWork;
            _businessRules = businessRules;
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
            var product = _productDal.Get(product => product.ProductId == id);
            return product == null
                ? new ErrorDataResult<Product>("Product was not found.")
                : new SuccessDataResult<Product>(product);
        }

        private IResult AddNewProduct(Product product)
        {
            IResult result = _businessRules.EnsureNameIsAvailable(product.Name);
            if (!result.Success) return result;

            var publishedStatus = _productStatusDal.Get(status => status.Code == "published");
            if (publishedStatus == null)
            {
                return new ErrorResult("Published product status is not configured.");
            }

            product.ProductStatusId = publishedStatus.Id;
            product.CreatedAt = DateTime.UtcNow;
            _productDal.Add(product);
            _unitOfWork.SaveChanges();
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

            var result = AddNewProduct(product);
            if (!result.Success)
            {
                return result;
            }

            var categoryResult = ReplaceCategories(product.ProductId, request.CategoryIds);
            if (!categoryResult.Success)
            {
                return categoryResult;
            }

            _unitOfWork.SaveChanges();
            return categoryResult;
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

            var duplicateResult = _businessRules.EnsureNameIsAvailable(product.Name, product.ProductId);
            if (!duplicateResult.Success)
            {
                return duplicateResult;
            }

            _productDal.Update(product);
            var categoryResult = ReplaceCategories(product.ProductId, request.CategoryIds);
            if (!categoryResult.Success)
            {
                return categoryResult;
            }

            _unitOfWork.SaveChanges();
            return categoryResult;
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
            _unitOfWork.SaveChanges();
            return new SuccessResult();
        }

        private IResult ReplaceCategories(int productId, IEnumerable<int> categoryIds)
        {
            var normalizedCategoryIds = categoryIds.Distinct().ToList();
            var categoryRule = _businessRules.EnsureCategoriesAreActive(normalizedCategoryIds);
            if (!categoryRule.Success)
            {
                return categoryRule;
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
