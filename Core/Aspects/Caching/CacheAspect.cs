using System;
using System.Reflection;
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
        private readonly ICachePolicyProvider _cachePolicyProvider;

        public CacheAspect(string profile, int duration = 60)
        {
            _duration = duration;
            _profile = profile;
            _cacheManager = ServiceTool.ServiceProvider.GetRequiredService<ICacheManager>();
            _cachePolicyProvider = ServiceTool.ServiceProvider.GetRequiredService<ICachePolicyProvider>();
        }

        public override void Intercept(IInvocation invocation)
        {
            var key = BuildCacheKey(invocation);
            var returnType = invocation.Method.ReturnType;
            if (_cacheManager.IsExists(key))
            {
                var cachedValue = _cacheManager.Get(key);
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

            _cacheManager.Add(key, value, _cachePolicyProvider.GetDurationMinutes(_profile, _duration));
        }

        private static bool IsGenericTask(Type returnType) =>
            returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>);

        private string BuildCacheKey(IInvocation invocation)
        {
            var methodName = $"{invocation.Method.ReflectedType?.FullName ?? invocation.Method.DeclaringType?.FullName ?? "Unknown"}.{invocation.Method.Name}";
            var arguments = JsonConvert.SerializeObject(invocation.Arguments);
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(arguments)));
            return $"{_profile}:{methodName}:{hash}";
        }
    }
}
