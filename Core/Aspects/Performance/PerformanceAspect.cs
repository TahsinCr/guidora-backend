using Castle.DynamicProxy;
using Core.Utilities.Interceptors.Autofac;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Aspects.Performance
{
    public class PerformanceAspect : MethodInterception
    {
        private readonly int _interval;

        public PerformanceAspect(int interval)
        {
            _interval = interval;
        }


        public override void Intercept(IInvocation invocation)
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                invocation.Proceed();
            }
            finally
            {
                stopwatch.Stop();
                if (stopwatch.Elapsed.TotalSeconds > _interval)
                {
                    Debug.WriteLine($"Performance : {invocation.Method.DeclaringType?.FullName ?? "<Unknown>"}.{invocation.Method.Name}-->{stopwatch.Elapsed.TotalSeconds}");
                }
            }
        }
    }
}
