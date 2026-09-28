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
            var returnType = invocation.Method.ReturnType;
            if (IsGenericTask(returnType))
            {
                var method = typeof(CacheAspect).GetMethod(nameof(InterceptGenericTaskCore),
                    BindingFlags.Instance | BindingFlags.NonPublic)!;
                invocation.ReturnValue = method.MakeGenericMethod(returnType.GetGenericArguments()[0])
                    .Invoke(this, [invocation])!;
                return;
            }

            var key = BuildCacheKey(invocation, _cacheManager.GetProfileGeneration(_profile));
            if (_cacheManager.TryGet(key, out var cachedValue) &&
                returnType.IsInstanceOfType(cachedValue))
            {
                invocation.ReturnValue = cachedValue;
                return;
            }

            invocation.Proceed();
            CacheIfSuccessful(key, invocation.ReturnValue);
        }

        private async Task<T> InterceptGenericTaskCore<T>(IInvocation invocation)
        {
            var generation = await _cacheManager.GetProfileGenerationAsync(_profile).ConfigureAwait(false);
            var key = BuildCacheKey(invocation, generation);
            var (found, cachedValue) = await _cacheManager.TryGetAsync(key).ConfigureAwait(false);
            if (found && cachedValue is T typedValue) return typedValue;

            invocation.Proceed();
            if (invocation.ReturnValue is not Task<T> pending)
                throw new InvalidOperationException("Cached asynchronous method did not return a Task.");
            var result = await pending.ConfigureAwait(false);
            if (result is not IResult operationResult || operationResult.Success)
                await _cacheManager.AddAsync(key, result, _duration > 0 ? _duration : null)
                    .ConfigureAwait(false);
            return result;
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

        private string BuildCacheKey(IInvocation invocation, long generation)
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
            return $"{_profile}:generation:{generation}:{methodName}:{hash}{string.Concat(keySegments)}";
        }
    }
}
