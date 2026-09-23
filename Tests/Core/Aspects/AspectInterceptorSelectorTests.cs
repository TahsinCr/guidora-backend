using Business.BusinessAspect.Autofac;
using Castle.DynamicProxy;
using Core.Aspects.Caching;
using Core.Aspects.Transaction;
using Core.CrossCuttingConcerns.Caching;
using Core.Utilities.Interceptors.Autofac;
using Core.Utilities.IoC;
using IResult = Core.Utilities.Results.IResult;
using SuccessResult = Core.Utilities.Results.SuccessResult;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Tests.Core.TestDoubles;

namespace Tests.Core.Aspects;

public sealed class AspectInterceptorSelectorTests
{
    [Fact]
    public void MethodAspectOverridesSameClassAspectAndPrioritiesAreDeterministic()
    {
        var services = new ServiceCollection()
            .AddSingleton<ICacheManager>(new CacheManagerStub())
            .AddSingleton<IHttpContextAccessor, HttpContextAccessor>()
            .BuildServiceProvider();
        ServiceTool.SetProvider(services);
        var method = typeof(AspectSelectionTarget).GetMethod(nameof(AspectSelectionTarget.Write))!;

        IInterceptor[] interceptors = new AspectInterceptorSelector().SelectInterceptors(
            typeof(AspectSelectionTarget), method, Array.Empty<IInterceptor>());

        Assert.Equal(
            [typeof(SecuredOperationAspect), typeof(CacheRemoveAspect), typeof(TransactionScopeAspect)],
            interceptors.Select(interceptor => interceptor.GetType()).ToArray());
        Assert.Equal(
            [100, 300, 400],
            interceptors.Cast<MethodInterceptionBaseAttribute>().Select(interceptor => interceptor.Priority).ToArray());
    }

    [Fact]
    public void CacheRemovalWrapsTransactionSoItRunsAfterCommit()
    {
        Assert.True(CacheRemoveAspect.InterceptorPriority < TransactionScopeAspect.InterceptorPriority);
    }
}

[SecuredOperationAspect("class.permission")]
public sealed class AspectSelectionTarget
{
    [SecuredOperationAspect("method.permission")]
    [CacheRemoveAspect("catalog")]
    [TransactionScopeAspect]
    public IResult Write() => new SuccessResult();
}
