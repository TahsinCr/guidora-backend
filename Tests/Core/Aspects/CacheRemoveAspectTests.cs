using Castle.DynamicProxy;
using Core.Aspects.Caching;
using Core.CrossCuttingConcerns.Caching;
using Core.Utilities.IoC;
using Core.Utilities.Results;
using Microsoft.Extensions.DependencyInjection;
using Tests.Core.TestDoubles;

namespace Tests.Core.Aspects;

public sealed class CacheRemoveAspectTests
{
    [Fact]
    public void DoesNotInvalidateWhenSynchronousBusinessResultFails()
    {
        var (proxy, cache) = CreateProxy();

        var result = proxy.Fail();

        Assert.False(result.Success);
        Assert.Empty(cache.InvalidatedPatterns);
    }

    [Fact]
    public void InvalidatesConfiguredProfilesAfterSuccessfulSynchronousCommand()
    {
        var (proxy, cache) = CreateProxy();

        var result = proxy.Succeed();

        Assert.True(result.Success);
        Assert.Equal(["catalog:*", "product-detail:*"], cache.InvalidatedPatterns);
    }

    [Fact]
    public async Task DoesNotInvalidateWhenAsynchronousBusinessResultFails()
    {
        var (proxy, cache) = CreateProxy();

        var result = await proxy.FailAsync();

        Assert.False(result.Success);
        Assert.Empty(cache.InvalidatedPatterns);
    }

    [Fact]
    public async Task InvalidatesAfterSuccessfulAsynchronousBusinessResult()
    {
        var (proxy, cache) = CreateProxy();

        var result = await proxy.SucceedAsync();

        Assert.True(result.Success);
        Assert.Equal(["catalog:*", "product-detail:*"], cache.InvalidatedPatterns);
    }

    [Fact]
    public void CanTargetInvalidationToOneArgumentValue()
    {
        var cache = new CacheManagerStub();
        var services = new ServiceCollection().AddSingleton<ICacheManager>(cache).BuildServiceProvider();
        ServiceTool.SetProvider(services);
        var proxy = new ProxyGenerator().CreateInterfaceProxyWithTarget<IWriteTarget>(
            new WriteTarget(), new CacheRemoveAspect("product-detail") { ArgumentIndex = 0 });

        Assert.True(proxy.UpdateProduct(42).Success);

        Assert.Equal($"product-detail:*:arg:0:{CacheKeySegment.Hash(42)}",
            Assert.Single(cache.InvalidatedPatterns));
    }

    private static (IWriteTarget Proxy, CacheManagerStub Cache) CreateProxy()
    {
        var cache = new CacheManagerStub();
        var services = new ServiceCollection().AddSingleton<ICacheManager>(cache).BuildServiceProvider();
        ServiceTool.SetProvider(services);
        var proxy = new ProxyGenerator().CreateInterfaceProxyWithTarget<IWriteTarget>(
            new WriteTarget(), new CacheRemoveAspect("catalog", "product-detail"));
        return (proxy, cache);
    }

    public interface IWriteTarget
    {
        IResult Fail();
        IResult Succeed();
        Task<IResult> FailAsync();
        Task<IResult> SucceedAsync();
        IResult UpdateProduct(int productId);
    }

    private sealed class WriteTarget : IWriteTarget
    {
        public IResult Fail() => new ErrorResult("rejected");
        public IResult Succeed() => new SuccessResult();
        public Task<IResult> FailAsync() => Task.FromResult<IResult>(new ErrorResult("rejected"));
        public Task<IResult> SucceedAsync() => Task.FromResult<IResult>(new SuccessResult());
        public IResult UpdateProduct(int productId) => new SuccessResult();
    }
}
