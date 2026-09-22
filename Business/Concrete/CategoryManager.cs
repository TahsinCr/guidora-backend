using Business.Abstract;
using Core.Business;
using Core.Utilities.Results;
using DataAccess.Abstract;
using Entities;
using System.Collections.Generic;
using Core.Aspects.Caching;
using Core.CrossCuttingConcerns.Caching;

namespace Business.Concrete
{
    public class CategoryManager : ICategoryService
    {
        private readonly ICategoryDal _categoryDal;

        public CategoryManager(ICategoryDal categoryDal)
        {
            _categoryDal = categoryDal;
        }

        [CacheAspect(CacheProfiles.CatalogNavigation, duration: 60)]
        public IDataResult<List<Category>> GetAll()
        {
            return new SuccessDataResult<List<Category>>(_categoryDal.GetAll());
        }

        [CacheAspect(CacheProfiles.CatalogNavigation, duration: 60)]
        public IDataResult<Category> GetById(int id)
        {
            return new SuccessDataResult<Category>(_categoryDal.Get(category => category.CategoryId == id));
        }

        [CacheRemoveAspect(CacheProfiles.CatalogNavigation)]
        public IResult Add(Category category)
        {
            _categoryDal.Add(category);
            return new SuccessResult();
        }

        [CacheRemoveAspect(CacheProfiles.CatalogNavigation)]
        public IResult Update(Category category)
        {
            _categoryDal.Update(category);
            return new SuccessResult();
        }

        [CacheRemoveAspect(CacheProfiles.CatalogNavigation)]
        public IResult Delete(Category category)
        {
            _categoryDal.Delete(category);
            return new SuccessResult();
        }
    }
}
