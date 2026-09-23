using Core.Utilities.Results;
using DataAccess.Abstract;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Business.BusinessRules.Catalog;

public sealed class ProductBusinessRules : IProductBusinessRules
{
    private readonly IProductDal _productDal;
    private readonly ICategoryDal _categoryDal;

    public ProductBusinessRules(IProductDal productDal, ICategoryDal categoryDal)
    {
        _productDal = productDal;
        _categoryDal = categoryDal;
    }

    public IResult EnsureNameIsAvailable(string name, int? excludingProductId = null)
    {
        var duplicate = _productDal.Get(product =>
            product.Name == name && (!excludingProductId.HasValue || product.ProductId != excludingProductId));

        return duplicate is null
            ? new SuccessResult()
            : new ErrorResult("A product with this name already exists.");
    }

    public IResult EnsureCategoriesAreActive(IEnumerable<int> categoryIds)
    {
        var requestedIds = categoryIds.Distinct().ToArray();
        if (requestedIds.Length == 0)
        {
            return new ErrorResult("At least one category is required.");
        }

        var activeIds = _categoryDal.GetAll(category => requestedIds.Contains(category.CategoryId) && category.IsActive)
            .Select(category => category.CategoryId)
            .ToHashSet();

        return activeIds.Count == requestedIds.Length
            ? new SuccessResult()
            : new ErrorResult("One or more categories are missing or inactive.");
    }

    public async Task<IResult> EnsureNameIsAvailableAsync(string name, int? excludingProductId = null,
        CancellationToken cancellationToken = default)
    {
        var duplicate = await _productDal.GetAsync(product =>
            product.Name == name && (!excludingProductId.HasValue || product.ProductId != excludingProductId),
            cancellationToken);
        return duplicate is null
            ? new SuccessResult()
            : new ErrorResult("A product with this name already exists.");
    }

    public async Task<IResult> EnsureCategoriesAreActiveAsync(IEnumerable<int> categoryIds,
        CancellationToken cancellationToken = default)
    {
        var requestedIds = categoryIds.Distinct().ToArray();
        if (requestedIds.Length == 0) return new ErrorResult("At least one category is required.");
        var activeIds = (await _categoryDal.GetAllAsync(
                category => requestedIds.Contains(category.CategoryId) && category.IsActive, cancellationToken))
            .Select(category => category.CategoryId).ToHashSet();
        return activeIds.Count == requestedIds.Length
            ? new SuccessResult()
            : new ErrorResult("One or more categories are missing or inactive.");
    }
}
