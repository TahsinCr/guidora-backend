using Core.Utilities.Results;
using DataAccess.Abstract;

namespace Business.BusinessRules.Catalog;

public sealed class CategoryBusinessRules : ICategoryBusinessRules
{
    private readonly ICategoryDal _categoryDal;

    public CategoryBusinessRules(ICategoryDal categoryDal) => _categoryDal = categoryDal;

    public IResult EnsureSlugIsAvailable(string slug, int? excludingCategoryId = null)
    {
        var duplicate = _categoryDal.Get(category =>
            category.Slug == slug && (!excludingCategoryId.HasValue || category.CategoryId != excludingCategoryId));

        return duplicate is null
            ? new SuccessResult()
            : new ErrorResult("A category with this slug already exists.");
    }
}
