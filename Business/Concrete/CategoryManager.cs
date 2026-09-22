using Business.Abstract;
using Core.Business;
using Core.Utilities.Results;
using DataAccess.Abstract;
using Entities;
using System.Collections.Generic;
using Core.Aspects.Caching;
using Core.CrossCuttingConcerns.Caching;
using Business.BusinessAspect.Autofac;
using Core.DataAccess;

namespace Business.Concrete
{
    public class CategoryManager : ICategoryService
    {
        private readonly ICategoryDal _categoryDal;
        private readonly IUnitOfWork _unitOfWork;

        public CategoryManager(ICategoryDal categoryDal, IUnitOfWork unitOfWork)
        {
            _categoryDal = categoryDal;
            _unitOfWork = unitOfWork;
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
        public IResult Add(Category category)
        {
            _categoryDal.Add(category);
            _unitOfWork.SaveChanges();
            return new SuccessResult();
        }

        [CacheRemoveAspect(CacheProfiles.CatalogNavigation)]
        [SecuredOperationAspect("Category.Manage")]
        public IResult Update(Category category)
        {
            _categoryDal.Update(category);
            _unitOfWork.SaveChanges();
            return new SuccessResult();
        }

        [CacheRemoveAspect(CacheProfiles.CatalogNavigation)]
        [SecuredOperationAspect("Category.Manage")]
        public IResult Delete(Category category)
        {
            _categoryDal.Delete(category);
            _unitOfWork.SaveChanges();
            return new SuccessResult();
        }
    }
}
