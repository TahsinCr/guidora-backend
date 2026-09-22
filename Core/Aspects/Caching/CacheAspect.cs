using Castle.DynamicProxy;
using Core.CrossCuttingConcerns.Caching;
using Core.Utilities.Interceptors.Autofac;
using Core.Utilities.IoC;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;
using Core.Utilities.Results;
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
            if (_cacheManager.IsExists(key))
            {
                invocation.ReturnValue = _cacheManager.Get(key);
                return;
            }
            invocation.Proceed();
            if (invocation.ReturnValue is IResult result && !result.Success)
            {
                return;
            }

            var duration = _cachePolicyProvider.GetDurationMinutes(_profile, _duration);
            _cacheManager.Add(key, invocation.ReturnValue, duration);
        }

        private string BuildCacheKey(IInvocation invocation)
        {
            var methodName = $"{invocation.Method.ReflectedType?.FullName ?? invocation.Method.DeclaringType?.FullName ?? "Unknown"}.{invocation.Method.Name}";
            var arguments = JsonConvert.SerializeObject(invocation.Arguments);
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(arguments)));
            return $"{_profile}:{methodName}:{hash}";
        }
    }
}
