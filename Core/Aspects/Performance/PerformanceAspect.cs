using System.Diagnostics;
using System.Reflection;
using System.Threading.Tasks;
using Castle.DynamicProxy;
using Core.Utilities.Interceptors.Autofac;

namespace Core.Aspects.Performance
{
    public class PerformanceAspect : MethodInterception
    {
        public const int InterceptorPriority = 500;
        private readonly int _interval;
        public PerformanceAspect(int interval)
        {
            Priority = InterceptorPriority;
            _interval = interval;
        }

        public override void Intercept(IInvocation invocation)
        {
            var stopwatch = Stopwatch.StartNew();
            invocation.Proceed();
            if (invocation.ReturnValue is Task task && invocation.Method.ReturnType.IsGenericType &&
                invocation.Method.ReturnType.GetGenericTypeDefinition() == typeof(Task<>))
            {
                invocation.ReturnValue = MeasureGenericTask(task, invocation, stopwatch);
                return;
            }

            if (invocation.ReturnValue is Task taskWithoutResult)
            {
                invocation.ReturnValue = MeasureTask(taskWithoutResult, invocation, stopwatch);
                return;
            }

            ReportIfSlow(invocation, stopwatch);
        }

        private async Task MeasureTask(Task task, IInvocation invocation, Stopwatch stopwatch)
        {
            try { await task.ConfigureAwait(false); }
            finally { ReportIfSlow(invocation, stopwatch); }
        }

        private object MeasureGenericTask(Task task, IInvocation invocation, Stopwatch stopwatch)
        {
            var method = typeof(PerformanceAspect).GetMethod(nameof(MeasureGenericTaskCore),
                BindingFlags.Instance | BindingFlags.NonPublic)!;
            return method.MakeGenericMethod(invocation.Method.ReturnType.GetGenericArguments()[0])
                .Invoke(this, [task, invocation, stopwatch])!;
        }

        private async Task<T> MeasureGenericTaskCore<T>(Task task, IInvocation invocation,
            Stopwatch stopwatch)
        {
            try { return await ((Task<T>)task).ConfigureAwait(false); }
            finally { ReportIfSlow(invocation, stopwatch); }
        }

        private void ReportIfSlow(IInvocation invocation, Stopwatch stopwatch)
        {
            stopwatch.Stop();
            if (stopwatch.Elapsed.TotalSeconds > _interval)
            {
                Debug.WriteLine($"Performance : {invocation.Method.DeclaringType?.FullName ?? "<Unknown>"}.{invocation.Method.Name}-->{stopwatch.Elapsed.TotalSeconds}");
            }
        }
    }
}
