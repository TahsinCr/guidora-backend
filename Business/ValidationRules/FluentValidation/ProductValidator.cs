using Entities;
using FluentValidation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Business.ValidationRules.FluentValidation
{
    public class ProductValidator: AbstractValidator<Product>
    {
        public ProductValidator()
        {
            RuleFor(p => p.Name).NotEmpty().MinimumLength(3).MaximumLength(200);
            RuleFor(p => p.Slug).NotEmpty().MaximumLength(220);
            RuleFor(p => p.ShortDescription).NotEmpty().MaximumLength(500);
            RuleFor(p => p.WebsiteUrl).NotEmpty().MaximumLength(2048)
                .Must(url => Uri.IsWellFormedUriString(url, UriKind.Absolute))
                .WithMessage("WebsiteUrl must be an absolute URL.");
            RuleFor(p => p.LogoUrl).NotEmpty().MaximumLength(2048)
                .Must(url => Uri.IsWellFormedUriString(url, UriKind.Absolute))
                .WithMessage("LogoUrl must be an absolute URL.");
        }
    }
}
