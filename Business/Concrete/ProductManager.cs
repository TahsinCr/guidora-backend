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

        public ProductManager(IProductDal productDal)
        {
            _productDal = productDal;
        }

        [CacheAspect(CacheProfiles.CatalogListing, duration: 10)]
        public IDataResult<List<Product>> GetAll()
        {
            return new SuccessDataResult<List<Product>>(_productDal.GetAll(), Messages.ProductsListed);
        }

        [CacheAspect(CacheProfiles.CatalogListing, duration: 10)]
        public IDataResult<List<Product>> GetAllByCategoryId(int id)
        {
            return new SuccessDataResult<List<Product>>(_productDal.GetAll(product => product.CategoryId == id));
        }


        [CacheAspect(CacheProfiles.ProductDetail, duration: 15)]
        public IDataResult<Product> GetById(int id)
        {
            return new SuccessDataResult<Product>(_productDal.Get(product=> product.ProductId == id));
        }

        [SecuredOperationAspect("product.add","admin")]
        [ValidationAspect(typeof(ProductValidator))]
        [CacheRemoveAspect(CacheProfiles.CatalogListing, CacheProfiles.ProductDetail)]
        public IResult Add(Product product)
        {
            IResult? result = BusinessRules.Run(new IResult[]{
                CheckIfProductCountOfCategoryCorrect(product.CategoryId),
                CheckIfProductNameAlreadyExists(product.ProductName),
                CheckIfCategoryLimitExceded(product.CategoryId, limit: 10)
            });
            if (result != null) return result;
            
            _productDal.Add(product);
            return new SuccessResult(Messages.ProductAdded);
            
        }

        [SecuredOperationAspect("product.update", "admin")]
        [ValidationAspect(typeof(ProductValidator))]
        [CacheRemoveAspect(CacheProfiles.CatalogListing, CacheProfiles.ProductDetail)]
        public IResult Update(Product entity)
        {
            _productDal.Update(entity);
            return new SuccessResult();
        }

        [SecuredOperationAspect("product.delete", "admin")]
        [ValidationAspect(typeof(ProductValidator))]
        [CacheRemoveAspect(CacheProfiles.CatalogListing, CacheProfiles.ProductDetail)]
        public IResult Delete(Product entity)
        {
            _productDal.Delete(entity);
            return new SuccessResult();
        }

        private IResult CheckIfProductCountOfCategoryCorrect(int categoryId)
        {
            int result = _productDal.GetAll(p => p.CategoryId == categoryId).Count;
            if (result == 15)
            {
                return new ErrorResult(Messages.ProductCountOfCategoryError);
            }

            return new SuccessResult();
        }

        private IResult CheckIfProductNameAlreadyExists(string productName)
        {
            bool result = _productDal.GetAll(p => p.ProductName == productName).Any();
            if (result)
            {
                return new ErrorResult(Messages.ProductNameAlreadyExistsError);
            }

            return new SuccessResult();
        }

        private IResult CheckIfCategoryLimitExceded(int categoryId, int limit)
        {
            var result = GetAllByCategoryId(categoryId);
            if (result.Data.Count > limit)
            {
                return new ErrorResult(Messages.CategoryLimitExcededError);
            }

            return new SuccessResult();
        }
    }
}
