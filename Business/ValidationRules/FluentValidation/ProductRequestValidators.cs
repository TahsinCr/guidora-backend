using System;
using Entities.DTOs;
using FluentValidation;

namespace Business.ValidationRules.FluentValidation
{
    public class CreateProductRequestValidator : AbstractValidator<CreateProductRequest>
    {
        public CreateProductRequestValidator()
        {
            RuleFor(request => request.Name).NotEmpty().MinimumLength(3).MaximumLength(200);
            RuleFor(request => request.Slug).NotEmpty().MaximumLength(220);
            RuleFor(request => request.ShortDescription).NotEmpty().MaximumLength(500);
            RuleFor(request => request.WebsiteUrl).NotEmpty().MaximumLength(2048)
                .Must(url => Uri.IsWellFormedUriString(url, UriKind.Absolute));
            RuleFor(request => request.LogoUrl).NotEmpty().MaximumLength(2048)
                .Must(url => Uri.IsWellFormedUriString(url, UriKind.Absolute));
            RuleFor(request => request.CategoryIds).NotEmpty();
        }
    }

    public class UpdateProductRequestValidator : AbstractValidator<UpdateProductRequest>
    {
        public UpdateProductRequestValidator()
        {
            RuleFor(request => request.Name).NotEmpty().MinimumLength(3).MaximumLength(200);
            RuleFor(request => request.Slug).NotEmpty().MaximumLength(220);
            RuleFor(request => request.ShortDescription).NotEmpty().MaximumLength(500);
            RuleFor(request => request.WebsiteUrl).NotEmpty().MaximumLength(2048)
                .Must(url => Uri.IsWellFormedUriString(url, UriKind.Absolute));
            RuleFor(request => request.LogoUrl).NotEmpty().MaximumLength(2048)
                .Must(url => Uri.IsWellFormedUriString(url, UriKind.Absolute));
            RuleFor(request => request.CategoryIds).NotEmpty();
        }
    }
}
