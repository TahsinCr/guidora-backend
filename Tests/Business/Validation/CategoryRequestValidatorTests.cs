using Business.ValidationRules.FluentValidation;
using Entities.DTOs;

namespace Tests.Business.Validation;

public sealed class CategoryRequestValidatorTests
{
    [Fact]
    public void RejectsMissingNameAndNonCanonicalSlug()
    {
        var result = new CategoryRequestValidator().Validate(new CategoryRequest
        {
            CategoryName = "",
            Slug = "Not A Slug",
            Description = ""
        });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(CategoryRequest.CategoryName));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(CategoryRequest.Slug));
    }

    [Fact]
    public void AcceptsValidCategoryRequest()
    {
        var result = new CategoryRequestValidator().Validate(new CategoryRequest
        {
            CategoryName = "Design tools",
            Slug = "design-tools",
            Description = "Tools for interface and visual design."
        });

        Assert.True(result.IsValid);
    }
}
