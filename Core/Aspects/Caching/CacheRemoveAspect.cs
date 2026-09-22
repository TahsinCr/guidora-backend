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

namespace Core.Aspects.Caching
{
    public class CacheRemoveAspect : MethodInterception
    {
        private readonly string[] _profiles;
        private readonly ICacheManager _cacheManager;

        public CacheRemoveAspect(params string[] profiles)
        {
            _profiles = profiles;
            _cacheManager = ServiceTool.ServiceProvider.GetRequiredService<ICacheManager>();
        }

        protected override void OnSuccess(IInvocation invocation)
        {
            foreach (string profile in _profiles)
            {
                _cacheManager.RemoveByPattern($"{profile}:*");
            }
        }
    }
}
