using Castle.DynamicProxy;
using System;
using System.Linq;
using System.Reflection;
using Core.Aspects.Logging;
using Core.Aspects.Performance;
using Core.CrossCuttingConcerns.Logging.Log4Net.Loggers;

namespace Core.Utilities.Interceptors.Autofac
{
    public class AspectInterceptorSelector : IInterceptorSelector
    {
        public IInterceptor[] SelectInterceptors(Type type, MethodInfo method, IInterceptor[] ınterceptors)
        {
            var classAttributes = type.GetCustomAttributes<MethodInterceptionBaseAttribute>(true)
                .ToList();
            var methodAttributes = (type.GetMethod(method.Name) ?? method)
                .GetCustomAttributes<MethodInterceptionBaseAttribute>(true)
                .ToList();
            classAttributes.AddRange(methodAttributes);
            //classAttributes.Add(new ExceptionLogAspect(typeof(FileLogger)));
            //classAttributes.Add(new PerformanceAspect(10));

            return classAttributes.OrderBy(x => x.Priority).ToArray();

        }
    }

}
