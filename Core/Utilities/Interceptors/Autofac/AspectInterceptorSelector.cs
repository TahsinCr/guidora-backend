using Castle.DynamicProxy;
using System;
using System.Linq;
using System.Reflection;

namespace Core.Utilities.Interceptors.Autofac
{
    public class AspectInterceptorSelector : IInterceptorSelector
    {
        public IInterceptor[] SelectInterceptors(Type type, MethodInfo method, IInterceptor[] ınterceptors)
        {
            var concreteMethod = type.GetMethod(method.Name, method.GetParameters().Select(parameter => parameter.ParameterType).ToArray()) ?? method;
            var classAttributes = type.GetCustomAttributes<MethodInterceptionBaseAttribute>(true).ToArray();
            var methodAttributes = concreteMethod.GetCustomAttributes<MethodInterceptionBaseAttribute>(true).ToArray();

            var methodAttributeTypes = methodAttributes.Select(attribute => attribute.GetType()).ToHashSet();
            var selected = classAttributes.Where(attribute => !methodAttributeTypes.Contains(attribute.GetType()))
                .Concat(methodAttributes)
                .Select((attribute, index) => new { Attribute = attribute, Index = index });

            return selected.OrderBy(item => item.Attribute.Priority)
                .ThenBy(item => item.Attribute.GetType().FullName, StringComparer.Ordinal)
                .ThenBy(item => item.Index)
                .Select(item => (Castle.DynamicProxy.IInterceptor)item.Attribute)
                .ToArray();

        }
    }

}
