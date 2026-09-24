using Entities.DTOs;
using FluentValidation;
using System;

namespace Business.ValidationRules.FluentValidation;

public sealed class UserForLoginValidator : AbstractValidator<UserForLoginDto>
{
    public UserForLoginValidator()
    {
        RuleFor(request => request.Email).NotEmpty().EmailAddress().MaximumLength(320);
        RuleFor(request => request.Password).NotEmpty().MaximumLength(128);
    }
}

public sealed class UserForRegisterValidator : AbstractValidator<UserForRegisterDto>
{
    public UserForRegisterValidator()
    {
        RuleFor(request => request.Email).NotEmpty().EmailAddress().MaximumLength(320);
        RuleFor(request => request.Password).NotEmpty().MinimumLength(12).MaximumLength(128);
        RuleFor(request => request.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(request => request.LastName).NotEmpty().MaximumLength(100);
    }
}

public sealed class CommentRequestValidator : AbstractValidator<CommentRequest>
{
    public CommentRequestValidator()
    {
        RuleFor(request => request.Content).NotEmpty().MaximumLength(10000);
    }
}

public sealed class ProductDocumentRequestValidator : AbstractValidator<ProductDocumentRequest>
{
    public ProductDocumentRequestValidator()
    {
        RuleFor(request => request.DocumentType).NotEmpty().MaximumLength(80);
        RuleFor(request => request.Title).NotEmpty().MaximumLength(250);
        RuleFor(request => request.Content).NotEmpty().MaximumLength(1_000_000);
    }
}

public sealed class ProductMediaRequestValidator : AbstractValidator<ProductMediaRequest>
{
    public ProductMediaRequestValidator()
    {
        RuleFor(request => request.MediaType).NotEmpty().MaximumLength(50);
        RuleFor(request => request.Url).NotEmpty().MaximumLength(2048).Must(IsHttpUrl);
        RuleFor(request => request.AltText).MaximumLength(500);
        RuleFor(request => request.DisplayOrder).GreaterThanOrEqualTo(0);
    }

    private static bool IsHttpUrl(string value) => Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && uri.Scheme is "http" or "https";
}

public sealed class ProductChangelogRequestValidator : AbstractValidator<ProductChangelogRequest>
{
    public ProductChangelogRequestValidator()
    {
        RuleFor(request => request.Version).NotEmpty().MaximumLength(80);
        RuleFor(request => request.Title).NotEmpty().MaximumLength(250);
        RuleFor(request => request.Description).MaximumLength(4000);
        RuleFor(request => request.Content).NotEmpty().MaximumLength(1_000_000);
        RuleFor(request => request.ReleaseDate).NotEmpty();
    }
}

public sealed class ProductContributorRequestValidator : AbstractValidator<ProductContributorRequest>
{
    public ProductContributorRequestValidator()
    {
        RuleFor(request => request.UserId).GreaterThan(0);
        RuleFor(request => request.ContributionType).NotEmpty().MaximumLength(100);
    }
}
