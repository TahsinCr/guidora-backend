using Business.Constants;
using Business.ValidationRules.FluentValidation;
using Entities.DTOs;

namespace Tests.Business.Catalog;

public sealed class ProductRequestValidationTests
{
    [Fact]
    public void CreateRequestAcceptsBoundedDistinctPositiveTaxonomySelections()
    {
        var result = new CreateProductRequestValidator().Validate(ValidCreateRequest());

        Assert.True(result.IsValid, string.Join("; ", result.Errors.Select(error => error.ErrorMessage)));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-1, 1)]
    [InlineData(1, 0)]
    [InlineData(1, -1)]
    public void CreateRequestRejectsNonPositiveCategoryAndTagIds(int categoryId, int tagId)
    {
        var request = ValidCreateRequest();
        request.CategoryIds = [categoryId];
        request.TagIds = [tagId];

        Assert.False(new CreateProductRequestValidator().Validate(request).IsValid);
    }

    [Fact]
    public void CreateRequestRejectsDuplicateOrExcessiveSelections()
    {
        var validator = new CreateProductRequestValidator();
        var duplicateCategory = ValidCreateRequest();
        duplicateCategory.CategoryIds = [1, 1];
        var tooManyCategories = ValidCreateRequest();
        tooManyCategories.CategoryIds = Enumerable.Range(1, ProductTaxonomySelectionPolicy.MaximumCategories + 1).ToList();
        var duplicateTag = ValidCreateRequest();
        duplicateTag.TagIds = [1, 1];
        var tooManyTags = ValidCreateRequest();
        tooManyTags.TagIds = Enumerable.Range(1, ProductTaxonomySelectionPolicy.MaximumTags + 1).ToList();

        Assert.False(validator.Validate(duplicateCategory).IsValid);
        Assert.False(validator.Validate(tooManyCategories).IsValid);
        Assert.False(validator.Validate(duplicateTag).IsValid);
        Assert.False(validator.Validate(tooManyTags).IsValid);
    }

    [Theory]
    [InlineData("ftp://example.test/logo.svg")]
    [InlineData("file:///tmp/logo.svg")]
    [InlineData("javascript:alert(1)")]
    public void ProductValidatorsRejectNonHttpLogoUrls(string logoUrl)
    {
        var create = ValidCreateRequest();
        create.LogoUrl = logoUrl;
        var update = new UpdateProductRequest
        {
            Name = create.Name,
            Slug = create.Slug,
            ShortDescription = create.ShortDescription,
            Description = create.Description,
            WebsiteUrl = create.WebsiteUrl,
            LogoUrl = logoUrl,
            CategoryIds = [1],
            TagIds = []
        };

        Assert.False(new CreateProductRequestValidator().Validate(create).IsValid);
        Assert.False(new UpdateProductRequestValidator().Validate(update).IsValid);
    }

    private static CreateProductRequest ValidCreateRequest() => new()
    {
        Name = "Valid product",
        Slug = "valid-product",
        ShortDescription = "Short description",
        Description = "Long description",
        WebsiteUrl = "https://example.test",
        LogoUrl = "https://example.test/logo.svg",
        CategoryIds = [1],
        TagIds = []
    };
}
