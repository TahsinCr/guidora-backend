using Entities.DTOs;
using FluentValidation;

namespace Business.ValidationRules.FluentValidation;

public sealed class TagRequestValidator : AbstractValidator<TagRequest>
{
    public TagRequestValidator()
    {
        RuleFor(request => request.TagCategoryId).GreaterThan(0);
        RuleFor(request => request.Name).NotEmpty().MaximumLength(120);
        RuleFor(request => request.Slug).NotEmpty().MaximumLength(140)
            .Matches("^[a-z0-9]+(?:-[a-z0-9]+)*$");
    }
}

public sealed class TagCategoryRequestValidator : AbstractValidator<TagCategoryRequest>
{
    public TagCategoryRequestValidator()
    {
        RuleFor(request => request.Name).NotEmpty().MaximumLength(120);
        RuleFor(request => request.Slug).NotEmpty().MaximumLength(140)
            .Matches("^[a-z0-9]+(?:-[a-z0-9]+)*$");
    }
}
