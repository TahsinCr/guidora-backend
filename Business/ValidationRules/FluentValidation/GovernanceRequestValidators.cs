using Core.Pagination;
using Entities.DTOs;
using FluentValidation;

namespace Business.ValidationRules.FluentValidation;

public sealed class ReviewNoteRequestValidator : AbstractValidator<ReviewNoteRequest>
{
    public ReviewNoteRequestValidator()
    {
        RuleFor(request => request.Note).MaximumLength(4000);
    }
}

public sealed class ProductReportRequestValidator : AbstractValidator<ProductReportRequest>
{
    public ProductReportRequestValidator()
    {
        RuleFor(request => request.ReasonId).GreaterThan(0);
        RuleFor(request => request.Description).NotEmpty()
            .Must(description => !string.IsNullOrWhiteSpace(description))
            .MaximumLength(4000);
    }
}

public sealed class PageRequestValidator : AbstractValidator<PageRequest>
{
    public PageRequestValidator()
    {
        RuleFor(request => request.Page).GreaterThan(0);
        RuleFor(request => request.PageSize).InclusiveBetween(1, 100);
    }
}
