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
    public void CachedSuccessResultPreservesItsPayloadThroughJsonSerialization()
    {
        var original = new SuccessDataResult<string[]>(["catalog-item"], "loaded");
        var serialized = Newtonsoft.Json.JsonConvert.SerializeObject(original);
        var restored = Newtonsoft.Json.JsonConvert.DeserializeObject<SuccessDataResult<string[]>>(serialized)!;
        Assert.Equal(original.Data, restored.Data);
        Assert.Equal(original.Message, restored.Message);
        Assert.True(restored.Success);
    }

    [Fact]
    public async Task ResumesAfterCacheReadWithoutReenteringTheInterceptor()
    {
        var (proxy, cache) = CreateProxy();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        cache.AsyncReadGate = gate.Task;
        var pending = proxy.GetAsync();
        Assert.False(pending.IsCompleted);
        gate.SetResult();

        var result = await pending.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("async", result.Message);
        Assert.Single(cache.Values);
        Assert.Equal("async", (await proxy.GetAsync()).Message);
    }

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
    public void VaryingByArgumentKeepsDifferentProductsInSeparateCacheEntries()
    {
        var cache = new CacheManagerStub();
        var services = new ServiceCollection().AddSingleton<ICacheManager>(cache).BuildServiceProvider();
        ServiceTool.SetProvider(services);
        var aspect = new CacheAspect("product-detail", 15) { VaryByArgumentIndexes = [0] };
        var proxy = new ProxyGenerator().CreateInterfaceProxyWithTarget<ICacheReadTarget>(
            new CacheReadTarget(), aspect);

        Assert.True(proxy.GetById(12).Success);
        Assert.True(proxy.GetById(27).Success);

        Assert.Equal(2, cache.RequestedDurations.Count);
        Assert.Contains(cache.Values.Keys, key => key.Contains(":arg:0:", StringComparison.Ordinal));
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
        IDataResult<string> GetById(int id);
    }

    private sealed class CacheReadTarget : ICacheReadTarget
    {
        public IDataResult<string> GetDefault() => new SuccessDataResult<string>("default");
        public IDataResult<string> GetForTenMinutes() => new SuccessDataResult<string>("ten");
        public IDataResult<string> GetError() => new ErrorDataResult<string>("not found");
        public Task<IDataResult<string>> GetAsync() => Task.FromResult<IDataResult<string>>(new SuccessDataResult<string>("async"));
        public IDataResult<string> GetById(int id) => new SuccessDataResult<string>(id.ToString());
    }
}
