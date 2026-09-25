using System;
using System.Reflection;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Castle.DynamicProxy;
using Core.CrossCuttingConcerns.Caching;
using Core.Utilities.Interceptors.Autofac;
using Core.Utilities.IoC;
using Core.Utilities.Results;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;

namespace Core.Aspects.Caching
{
    public class CacheAspect : MethodInterception
    {
        private readonly int _duration;
        private readonly string _profile;
        private readonly ICacheManager _cacheManager;

        public CacheAspect(string profile, int duration = 0)
        {
            _duration = duration;
            _profile = profile;
            _cacheManager = ServiceTool.ServiceProvider.GetRequiredService<ICacheManager>();
            Priority = InterceptorPriority;
        }

        public const int InterceptorPriority = 300;

        public int[] VaryByArgumentIndexes { get; set; } = [];

        public override void Intercept(IInvocation invocation)
        {
            var key = BuildCacheKey(invocation);
            var returnType = invocation.Method.ReturnType;
            if (_cacheManager.TryGet(key, out var cachedValue))
            {
                invocation.ReturnValue = IsGenericTask(returnType)
                    ? CreateCompletedTask(returnType.GetGenericArguments()[0], cachedValue)
                    : cachedValue;
                return;
            }

            invocation.Proceed();
            if (invocation.ReturnValue is Task task && IsGenericTask(returnType))
            {
                invocation.ReturnValue = CacheGenericTask(task, returnType.GetGenericArguments()[0], key);
                return;
            }

            CacheIfSuccessful(key, invocation.ReturnValue);
        }

        private object CacheGenericTask(Task task, Type resultType, string key)
        {
            var method = typeof(CacheAspect).GetMethod(nameof(CacheGenericTaskCore),
                BindingFlags.Instance | BindingFlags.NonPublic)!;
            return method.MakeGenericMethod(resultType).Invoke(this, [task, key])!;
        }

        private async Task<T> CacheGenericTaskCore<T>(Task task, string key)
        {
            var result = await ((Task<T>)task).ConfigureAwait(false);
            CacheIfSuccessful(key, result);
            return result;
        }

        private static object CreateCompletedTask(Type resultType, object? value)
        {
            var method = typeof(Task).GetMethod(nameof(Task.FromResult))!.MakeGenericMethod(resultType);
            return method.Invoke(null, [value])!;
        }

        private void CacheIfSuccessful(string key, object? value)
        {
            if (value is IResult result && !result.Success)
            {
                return;
            }

            _cacheManager.Add(key, value, _duration > 0 ? _duration : null);
        }

        private static bool IsGenericTask(Type returnType) =>
            returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>);

        private string BuildCacheKey(IInvocation invocation)
        {
            var methodName = $"{invocation.Method.ReflectedType?.FullName ?? invocation.Method.DeclaringType?.FullName ?? "Unknown"}.{invocation.Method.Name}";
            var cacheArguments = invocation.Arguments
                .Where((_, index) => invocation.Method.GetParameters()[index].ParameterType != typeof(System.Threading.CancellationToken))
                .ToArray();
            var arguments = JsonConvert.SerializeObject(cacheArguments);
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(arguments)));
            var keySegments = VaryByArgumentIndexes
                .Where(index =>
                {
                    if (index < 0 || index >= invocation.Arguments.Length)
                    {
                        throw new InvalidOperationException(
                            $"Cache argument index {index} is outside the parameter list for '{methodName}'.");
                    }

                    return invocation.Method.GetParameters()[index].ParameterType != typeof(System.Threading.CancellationToken);
                })
                .Select(index =>
            {
                return $":arg:{index}:{CacheKeySegment.Hash(invocation.Arguments[index])}";
            });
            var generation = _cacheManager.GetProfileGeneration(_profile);
            return $"{_profile}:generation:{generation}:{methodName}:{hash}{string.Concat(keySegments)}";
        }
    }
}
