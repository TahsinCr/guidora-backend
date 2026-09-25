using Castle.DynamicProxy;
using System.Threading.Tasks;
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

            if (result is ICacheInvalidationDirective directive)
            {
                foreach (var target in directive.CacheInvalidations)
                {
                    var pattern = target.ArgumentIndex is int argumentIndex
                        ? $"{target.Profile}:*:arg:{argumentIndex}:{CacheKeySegment.Hash(target.ArgumentValue)}"
                        : string.Empty;
                    if (target.ArgumentIndex is int)
                        _cacheManager.RemoveByPattern(pattern);
                    else
                        _cacheManager.AdvanceProfileGeneration(target.Profile);
                }

                return;
            }

            foreach (string profile in _profiles)
            {
                if (ArgumentIndex >= 0 && ArgumentIndex < invocation.Arguments.Length)
                    _cacheManager.RemoveByPattern($"{profile}:*:arg:{ArgumentIndex}:{CacheKeySegment.Hash(invocation.Arguments[ArgumentIndex])}");
                else
                    _cacheManager.AdvanceProfileGeneration(profile);
            }
        }

        protected override async Task OnSuccessAsync(IInvocation invocation, object? result)
        {
            if (result is IResult operationResult && !operationResult.Success) return;

            if (result is ICacheInvalidationDirective directive)
            {
                foreach (var target in directive.CacheInvalidations)
                {
                    if (target.ArgumentIndex is int argumentIndex)
                        await _cacheManager.RemoveByPatternAsync(
                            $"{target.Profile}:*:arg:{argumentIndex}:{CacheKeySegment.Hash(target.ArgumentValue)}")
                            .ConfigureAwait(false);
                    else
                        await _cacheManager.AdvanceProfileGenerationAsync(target.Profile).ConfigureAwait(false);
                }
                return;
            }

            foreach (var profile in _profiles)
            {
                if (ArgumentIndex >= 0 && ArgumentIndex < invocation.Arguments.Length)
                    await _cacheManager.RemoveByPatternAsync(
                        $"{profile}:*:arg:{ArgumentIndex}:{CacheKeySegment.Hash(invocation.Arguments[ArgumentIndex])}")
                        .ConfigureAwait(false);
                else
                    await _cacheManager.AdvanceProfileGenerationAsync(profile).ConfigureAwait(false);
            }
        }
    }
}
