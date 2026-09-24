using Core.Utilities.Results;
using DataAccess.Abstract;
using System.Threading;
using System.Threading.Tasks;

namespace Business.BusinessRules.Catalog;

public sealed class CategoryBusinessRules : ICategoryBusinessRules
{
    private readonly ICategoryDal _categoryDal;

    public CategoryBusinessRules(ICategoryDal categoryDal) => _categoryDal = categoryDal;

    public async Task<IResult> EnsureSlugIsAvailableAsync(string slug, int? excludingCategoryId = null,
        CancellationToken cancellationToken = default)
    {
        var duplicate = await _categoryDal.GetAsync(category =>
            category.Slug == slug && (!excludingCategoryId.HasValue || category.CategoryId != excludingCategoryId),
            cancellationToken);
        return duplicate is null
            ? new SuccessResult()
            : new ErrorResult("A category with this slug already exists.");
    }
}
