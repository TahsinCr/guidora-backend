using Castle.DynamicProxy;
using Core.Aspects.Caching;
using Core.CrossCuttingConcerns.Caching;
using Core.Utilities.IoC;
using Core.Utilities.Results;
using Microsoft.Extensions.DependencyInjection;
using Tests.Core.TestDoubles;

namespace Tests.Core.Aspects;

public sealed class CacheAspectTests
{
    [Fact]
    public void OmittingTtlStoresEntryWithoutTimeExpiration()
    {
        var (proxy, cache) = CreateProxy();

        Assert.True(proxy.GetDefault().Success);

        Assert.Single(cache.RequestedDurations);
        Assert.Null(cache.RequestedDurations.Values.Single());
    }

    [Fact]
    public void ExplicitTtlIsTakenFromTheAspectArgument()
    {
        var (proxy, cache) = CreateProxy(duration: 10);

        Assert.True(proxy.GetForTenMinutes().Success);

        Assert.Equal(10, cache.RequestedDurations.Values.Single());
    }

    [Fact]
    public async Task CachesAsynchronousResultsAfterTheCallCompletes()
    {
        var (proxy, cache) = CreateProxy();

        Assert.True((await proxy.GetAsync()).Success);

        Assert.Single(cache.RequestedDurations);
    }

    [Fact]
    public void DoesNotCacheBusinessErrorResults()
    {
        var (proxy, cache) = CreateProxy();

        Assert.False(proxy.GetError().Success);

        Assert.Empty(cache.RequestedDurations);
    }

    private static (ICacheReadTarget Proxy, CacheManagerStub Cache) CreateProxy(int duration = 0)
    {
        var cache = new CacheManagerStub();
        var services = new ServiceCollection().AddSingleton<ICacheManager>(cache).BuildServiceProvider();
        ServiceTool.SetProvider(services);
        var generator = new ProxyGenerator();
        var target = new CacheReadTarget();
        return (generator.CreateInterfaceProxyWithTarget<ICacheReadTarget>(target,
                new CacheAspect("test-profile", duration)), cache);
    }

    public interface ICacheReadTarget
    {
        IDataResult<string> GetDefault();
        IDataResult<string> GetForTenMinutes();
        IDataResult<string> GetError();
        Task<IDataResult<string>> GetAsync();
    }

    private sealed class CacheReadTarget : ICacheReadTarget
    {
        public IDataResult<string> GetDefault() => new SuccessDataResult<string>("default");
        public IDataResult<string> GetForTenMinutes() => new SuccessDataResult<string>("ten");
        public IDataResult<string> GetError() => new ErrorDataResult<string>("not found");
        public Task<IDataResult<string>> GetAsync() => Task.FromResult<IDataResult<string>>(new SuccessDataResult<string>("async"));
    }
}
