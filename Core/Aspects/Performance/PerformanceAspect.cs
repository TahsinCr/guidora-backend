using System.Diagnostics;
using System.Reflection;
using System.Threading.Tasks;
using Castle.DynamicProxy;
using Core.Utilities.IoC;
using Core.Utilities.Interceptors.Autofac;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Core.Aspects.Performance
{
    public class PerformanceAspect : MethodInterception
    {
        // Performance wraps cache and transaction interceptors so elapsed time includes
        // cache hits, database commit, and post-commit cache invalidation.
        public const int InterceptorPriority = 250;
        private readonly int _interval;
        private readonly ILogger<PerformanceAspect> _logger;

        public PerformanceAspect(int interval)
            : this(interval, ServiceTool.ServiceProvider?.GetService<ILogger<PerformanceAspect>>()
                            ?? NullLogger<PerformanceAspect>.Instance)
        {
        }

        internal PerformanceAspect(int interval, ILogger<PerformanceAspect> logger)
        {
            Priority = InterceptorPriority;
            _interval = interval;
            _logger = logger;
        }

        public override void Intercept(IInvocation invocation)
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                invocation.Proceed();
            }
            catch
            {
                ReportIfSlow(invocation, stopwatch);
                throw;
            }

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
                _logger.LogWarning(
                    "Slow operation {OperationName} took {ElapsedMilliseconds} ms, exceeding the {ThresholdSeconds} second threshold.",
                    $"{invocation.Method.DeclaringType?.FullName ?? "<Unknown>"}.{invocation.Method.Name}",
                    stopwatch.Elapsed.TotalMilliseconds,
                    _interval);
            }
        }
    }
}
