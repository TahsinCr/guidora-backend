
using Castle.DynamicProxy;
using Core.Entities;
using FluentValidation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Core.CrossCuttingConcerns.Validation;
using Core.Utilities.Interceptors.Autofac;

namespace Core.Aspects.Autofac.Validation
{
    public class ValidationAspect : MethodInterception
    {
        public const int InterceptorPriority = 200;
        private readonly Type _validatorType;
        public ValidationAspect(Type validatorType)
        {
            Priority = InterceptorPriority;
            if (!typeof(IValidator).IsAssignableFrom(validatorType))
            {
                throw new System.Exception("This is not the right class");
            }
            _validatorType = validatorType;
        }

        protected override void OnBefore(IInvocation invocation)
        {
            var validator = Activator.CreateInstance(_validatorType) as IValidator
                ?? throw new InvalidOperationException("Unable to create the validator.");
            var entityType = _validatorType.BaseType?.GetGenericArguments().FirstOrDefault()
                ?? throw new InvalidOperationException("The validator must inherit from a generic validator type.");
            var entities = invocation.Arguments
                .Where(t => t is not null && t.GetType() == entityType)
                .Select(t => t!);
            foreach ( var entity in entities)
            {
                ValidationTool.FluentValidation(validator, entity);
            }
        }
    }
}
