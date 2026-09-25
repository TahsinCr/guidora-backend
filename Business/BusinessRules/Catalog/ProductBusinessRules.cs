using Core.Utilities.Results;
using DataAccess.Abstract;
using System.Linq;
using System.Collections.Generic;
using System;
using System.Threading;
using System.Threading.Tasks;
using Business.Utilities;

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

    public async Task<IResult> EnsureIdentityIsAvailableAsync(string name, string slug, string websiteUrl,
        int? excludingProductId = null, CancellationToken cancellationToken = default)
    {
        string normalizedName;
        string normalizedSlug;
        string normalizedWebsiteUrl;
        try
        {
            normalizedName = ProductIdentityNormalizer.NormalizeName(name);
            normalizedSlug = ProductIdentityNormalizer.NormalizeSlug(slug);
            normalizedWebsiteUrl = ProductIdentityNormalizer.NormalizeWebsiteUrl(websiteUrl);
        }
        catch (ArgumentException)
        {
            return new ErrorResult("Product identity fields are invalid.");
        }

        var duplicate = await _productDal.GetAsync(product =>
            (!excludingProductId.HasValue || product.ProductId != excludingProductId) &&
            (product.Name == normalizedName ||
             product.Slug == normalizedSlug ||
             product.WebsiteUrl == normalizedWebsiteUrl), cancellationToken);

        return duplicate is null
            ? new SuccessResult()
            : new Core.Utilities.Results.ConflictResult("A Product with the same name, slug, or website URL already exists.");
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

    public async Task<IResult> EnsureCategoryIsActiveAsync(int categoryId,
        CancellationToken cancellationToken = default)
    {
        var isActive = await _categoryDal.AnyAsync(category =>
            category.CategoryId == categoryId && category.IsActive, cancellationToken);

        return isActive
            ? new SuccessResult()
            : new Core.Utilities.Results.NotFoundResult("Category was not found.");
    }
}
