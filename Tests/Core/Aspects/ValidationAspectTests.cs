using Castle.DynamicProxy;
using Core.Aspects.Autofac.Validation;
using Core.Utilities.IoC;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Tests.Core.Aspects;

public sealed class ValidationAspectTests
{
    [Fact]
    public void ResolvesAValidatorWithScopedDependenciesThroughAnIntermediateGenericBase()
    {
        using var rootProvider = CreateProvider();
        using var requestScope = rootProvider.CreateScope();
        var (proxy, target) = CreateProxy(rootProvider, requestScope);

        proxy.Submit(new ValidationRequest("valid"));

        Assert.True(target.WasInvoked);
    }

    [Fact]
    public void RejectsAnInvalidRequestBeforeCallingTheTarget()
    {
        using var rootProvider = CreateProvider();
        using var requestScope = rootProvider.CreateScope();
        var (proxy, target) = CreateProxy(rootProvider, requestScope);

        var exception = Assert.Throws<ValidationException>(() => proxy.Submit(new ValidationRequest("invalid")));

        Assert.Contains(exception.Errors, error => error.PropertyName == nameof(ValidationRequest.Value));
        Assert.False(target.WasInvoked);
    }

    private static ServiceProvider CreateProvider() => new ServiceCollection()
        .AddHttpContextAccessor()
        .AddScoped<ValidationDependency>()
        .AddScoped<IValidator<ValidationRequest>, ValidationRequestValidator>()
        .BuildServiceProvider(validateScopes: true);

    private static (IValidationTarget Proxy, ValidationTarget Target) CreateProxy(
        ServiceProvider rootProvider, IServiceScope requestScope)
    {
        rootProvider.GetRequiredService<IHttpContextAccessor>().HttpContext = new DefaultHttpContext
        {
            RequestServices = requestScope.ServiceProvider
        };
        ServiceTool.SetProvider(rootProvider);
        var target = new ValidationTarget();
        var proxy = new ProxyGenerator().CreateInterfaceProxyWithTarget<IValidationTarget>(
            target, new ValidationAspect(typeof(ValidationRequestValidator)));
        return (proxy, target);
    }

    public interface IValidationTarget
    {
        void Submit(ValidationRequest request);
    }

    public sealed record ValidationRequest(string Value);

    public sealed class ValidationTarget : IValidationTarget
    {
        public bool WasInvoked { get; private set; }

        public void Submit(ValidationRequest request) => WasInvoked = true;
    }

    public sealed class ValidationDependency
    {
        public bool Accepts(string value) => value == "valid";
    }

    public abstract class ValidationRequestValidatorBase<TRequest> : AbstractValidator<TRequest>
    {
    }

    public sealed class ValidationRequestValidator : ValidationRequestValidatorBase<ValidationRequest>
    {
        public ValidationRequestValidator(ValidationDependency dependency)
        {
            RuleFor(request => request.Value)
                .Must(dependency.Accepts)
                .WithMessage("The request value is invalid.");
        }
    }
}
