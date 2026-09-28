using System;
using Entities.DTOs;
using Entities;
using FluentValidation;
using Business.Constants;
using Business.ValidationRules;

namespace Business.ValidationRules.FluentValidation
{
    public class CreateProductRequestValidator : AbstractValidator<CreateProductRequest>
    {
        public CreateProductRequestValidator()
        {
            RuleFor(request => request.Name).NotEmpty().MinimumLength(3).MaximumLength(200);
            RuleFor(request => request.Slug).NotEmpty().MaximumLength(220);
            RuleFor(request => request.ShortDescription).NotEmpty().MaximumLength(500);
            RuleFor(request => request.Description).NotEmpty()
                .MaximumLength(ProductDescriptionPolicy.MaximumLength);
            RuleFor(request => request.WebsiteUrl).NotEmpty().MaximumLength(2048)
                .Must(url => Uri.IsWellFormedUriString(url, UriKind.Absolute));
            RuleFor(request => request.LogoUrl).NotEmpty().MaximumLength(2048)
                .Must(HttpUrlPolicy.IsValid);
            RuleFor(request => request.CategoryIds).Must(ProductTaxonomySelectionPolicy.HasValidCategoryIds)
                .WithMessage($"Select between 1 and {ProductTaxonomySelectionPolicy.MaximumCategories} distinct positive category IDs.");
            RuleFor(request => request.TagIds).Must(ProductTaxonomySelectionPolicy.HasValidTagIds)
                .WithMessage($"Select at most {ProductTaxonomySelectionPolicy.MaximumTags} distinct positive tag IDs.");
        }
    }

    public class UpdateProductRequestValidator : AbstractValidator<UpdateProductRequest>
    {
        public UpdateProductRequestValidator()
        {
            RuleFor(request => request.Name).NotEmpty().MinimumLength(3).MaximumLength(200);
            RuleFor(request => request.Slug).NotEmpty().MaximumLength(220);
            RuleFor(request => request.ShortDescription).NotEmpty().MaximumLength(500);
            RuleFor(request => request.Description).NotEmpty()
                .MaximumLength(ProductDescriptionPolicy.MaximumLength);
            RuleFor(request => request.WebsiteUrl).NotEmpty().MaximumLength(2048)
                .Must(url => Uri.IsWellFormedUriString(url, UriKind.Absolute));
            RuleFor(request => request.LogoUrl).NotEmpty().MaximumLength(2048)
                .Must(HttpUrlPolicy.IsValid);
            RuleFor(request => request.CategoryIds).Must(ProductTaxonomySelectionPolicy.HasValidCategoryIds)
                .WithMessage($"Select between 1 and {ProductTaxonomySelectionPolicy.MaximumCategories} distinct positive category IDs.");
            RuleFor(request => request.TagIds).Must(ProductTaxonomySelectionPolicy.HasValidTagIds)
                .WithMessage($"Select at most {ProductTaxonomySelectionPolicy.MaximumTags} distinct positive tag IDs.");
        }
    }
}
