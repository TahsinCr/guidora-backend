using Entities.DTOs;
using FluentValidation;

namespace Business.ValidationRules.FluentValidation;

public sealed class RoleRequestValidator : AbstractValidator<RoleRequest>
{
    public RoleRequestValidator()
    {
        RuleFor(request => request.Name).NotEmpty().MaximumLength(100).Must(value => value == value.Trim());
        RuleFor(request => request.Code).NotEmpty().MaximumLength(100)
            .Matches("^[a-z0-9]+(?:-[a-z0-9]+)*$");
    }
}
