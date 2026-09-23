using Entities.DTOs;
using FluentValidation;

namespace Business.ValidationRules.FluentValidation;

public sealed class CategoryRequestValidator : AbstractValidator<CategoryRequest>
{
    public CategoryRequestValidator()
    {
        RuleFor(request => request.CategoryName).NotEmpty().MaximumLength(150);
        RuleFor(request => request.Slug).NotEmpty().MaximumLength(180)
            .Matches("^[a-z0-9]+(?:-[a-z0-9]+)*$");
        RuleFor(request => request.Description).MaximumLength(4000);
    }
}
