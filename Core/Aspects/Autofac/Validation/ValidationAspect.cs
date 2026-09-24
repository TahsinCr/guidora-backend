
using Castle.DynamicProxy;
using FluentValidation;
using System;
using System.Linq;
using Core.CrossCuttingConcerns.Validation;
using Core.Utilities.Interceptors.Autofac;
using Core.Utilities.IoC;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Core.Aspects.Autofac.Validation
{
    public class ValidationAspect : MethodInterception
    {
        public const int InterceptorPriority = 200;
        private readonly Type _validatorServiceType;

        public ValidationAspect(Type validatorType)
        {
            Priority = InterceptorPriority;
            if (!typeof(IValidator).IsAssignableFrom(validatorType))
            {
                throw new ArgumentException("The validator type must implement FluentValidation.IValidator.",
                    nameof(validatorType));
            }

            _validatorServiceType = validatorType.GetInterfaces()
                .SingleOrDefault(type => type.IsGenericType &&
                                         type.GetGenericTypeDefinition() == typeof(IValidator<>))
                ?? throw new ArgumentException("The validator must implement IValidator<T>.", nameof(validatorType));
        }

        protected override void OnBefore(IInvocation invocation)
        {
            var rootProvider = ServiceTool.ServiceProvider;
            var requestServices = rootProvider.GetService<IHttpContextAccessor>()?.HttpContext?.RequestServices;
            var validatorProvider = requestServices ?? rootProvider;
            var validator = validatorProvider.GetRequiredService(_validatorServiceType) as IValidator
                ?? throw new InvalidOperationException(
                    $"Validator service '{_validatorServiceType}' is not registered.");
            var entityType = _validatorServiceType.GetGenericArguments()[0];
            var entities = invocation.Arguments
                .Where(entityType.IsInstanceOfType)
                .Select(t => t!);
            foreach (var entity in entities)
            {
                ValidationTool.FluentValidation(validator, entity);
            }
        }
    }
}
