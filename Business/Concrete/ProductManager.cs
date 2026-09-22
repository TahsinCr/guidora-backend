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
using Core.Business;
using Core.Entities;
using Core.Utilities.Results;
using DataAccess.Abstract;
using Entities;

namespace Business.Concrete
{
    public class ProductManager : IProductService
    {
        private readonly IProductDal _productDal;
        private readonly IProductCategoryDal _productCategoryDal;
        private readonly IProductStatusDal _productStatusDal;

        public ProductManager(IProductDal productDal, IProductCategoryDal productCategoryDal,
            IProductStatusDal productStatusDal)
        {
            _productDal = productDal;
            _productCategoryDal = productCategoryDal;
            _productStatusDal = productStatusDal;
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
            IResult? result = BusinessRules.Run(CheckIfProductNameAlreadyExists(product.Name));
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

        private IResult CheckIfProductNameAlreadyExists(string productName)
        {
            bool result = _productDal.GetAll(p => p.Name == productName).Any();
            if (result)
            {
                return new ErrorResult(Messages.ProductNameAlreadyExistsError);
            }

            return new SuccessResult();
        }
    }
}
