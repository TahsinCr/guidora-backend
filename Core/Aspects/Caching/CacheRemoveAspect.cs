using Castle.DynamicProxy;
using Core.CrossCuttingConcerns.Caching;
using Core.Utilities.Interceptors.Autofac;
using Core.Utilities.IoC;
using Microsoft.Extensions.DependencyInjection;
using Core.Utilities.Results;

namespace Core.Aspects.Caching
{
    public class CacheRemoveAspect : MethodInterception
    {
        public const int InterceptorPriority = 300;
        private readonly string[] _profiles;
        private readonly ICacheManager _cacheManager;

        public int ArgumentIndex { get; set; } = -1;

        public CacheRemoveAspect(params string[] profiles)
        {
            Priority = InterceptorPriority;
            _profiles = profiles;
            _cacheManager = ServiceTool.ServiceProvider.GetRequiredService<ICacheManager>();
        }

        protected override void OnSuccess(IInvocation invocation, object? result)
        {
            if (result is IResult operationResult && !operationResult.Success)
            {
                return;
            }

            foreach (string profile in _profiles)
            {
                var pattern = ArgumentIndex >= 0 && ArgumentIndex < invocation.Arguments.Length
                    ? $"{profile}:*:arg:{ArgumentIndex}:{CacheKeySegment.Hash(invocation.Arguments[ArgumentIndex])}"
                    : $"{profile}:*";
                _cacheManager.RemoveByPattern(pattern);
            }
        }
    }
}
