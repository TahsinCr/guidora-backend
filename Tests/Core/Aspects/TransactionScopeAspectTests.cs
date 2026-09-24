using System.Transactions;
using Castle.DynamicProxy;
using Core.Aspects.Transaction;
using Core.Aspects.Caching;
using Core.CrossCuttingConcerns.Caching;
using Core.Utilities.IoC;
using Core.Utilities.Interceptors.Autofac;
using Core.Utilities.Results;
using Microsoft.Extensions.DependencyInjection;
using Tests.Core.TestDoubles;

namespace Tests.Core.Aspects;

public sealed class TransactionScopeAspectTests
{
    [Fact]
    public void CommitsWhenSynchronousCommandSucceeds()
    {
        var probe = new TransactionProbe();
        var proxy = CreateProxy(probe);

        Assert.True(proxy.Execute(success: true).Success);
        Assert.True(probe.Committed);
        Assert.False(probe.RolledBack);
    }

    [Fact]
    public void RollsBackWhenSynchronousCommandReturnsErrorResult()
    {
        var probe = new TransactionProbe();
        var proxy = CreateProxy(probe);

        Assert.False(proxy.Execute(success: false).Success);
        Assert.False(probe.Committed);
        Assert.True(probe.RolledBack);
    }

    [Fact]
    public async Task RollsBackWhenAsynchronousCommandReturnsErrorResult()
    {
        var probe = new TransactionProbe();
        var proxy = CreateProxy(probe);

        Assert.False((await proxy.ExecuteAsync(success: false)).Success);
        Assert.False(probe.Committed);
        Assert.True(probe.RolledBack);
    }

    [Fact]
    public async Task CommitsWhenAsynchronousCommandSucceeds()
    {
        var probe = new TransactionProbe();
        var proxy = CreateProxy(probe);

        Assert.True((await proxy.ExecuteAsync(success: true)).Success);
        Assert.True(probe.Committed);
        Assert.False(probe.RolledBack);
    }

    [Fact]
    public void CacheInvalidationRunsAfterDatabaseTransactionCommit()
    {
        var events = new List<string>();
        var cache = new CacheManagerStub(events);
        ServiceTool.SetProvider(new ServiceCollection().AddSingleton<ICacheManager>(cache).BuildServiceProvider());
        var probe = new TransactionProbe(events);
        var proxy = CreateCacheProxy(probe);

        Assert.True(proxy.Execute(success: true).Success);

        Assert.Equal(["commit", $"invalidate:product-detail:*:arg:0:{CacheKeySegment.Hash(42)}"], events);
    }

    [Fact]
    public void CacheInvalidationDoesNotRunAfterRolledBackBusinessResult()
    {
        var events = new List<string>();
        var cache = new CacheManagerStub(events);
        ServiceTool.SetProvider(new ServiceCollection().AddSingleton<ICacheManager>(cache).BuildServiceProvider());
        var probe = new TransactionProbe(events);
        var proxy = CreateCacheProxy(probe);

        Assert.False(proxy.Execute(success: false).Success);

        Assert.Equal(["rollback"], events);
    }

    private static ITransactionalTarget CreateProxy(TransactionProbe probe) =>
        new ProxyGenerator().CreateInterfaceProxyWithTarget<ITransactionalTarget>(
            new TransactionalTarget(probe), new TransactionScopeAspect());

    private static ICacheTransactionalTarget CreateCacheProxy(TransactionProbe probe)
    {
        var target = new CacheTransactionalTarget(probe);
        var method = typeof(CacheTransactionalTarget).GetMethod(nameof(CacheTransactionalTarget.Execute))!;
        var interceptors = new AspectInterceptorSelector().SelectInterceptors(
            typeof(CacheTransactionalTarget), method, Array.Empty<IInterceptor>());
        return new ProxyGenerator().CreateInterfaceProxyWithTarget<ICacheTransactionalTarget>(target, interceptors);
    }

    public interface ITransactionalTarget
    {
        IResult Execute(bool success);
        Task<IResult> ExecuteAsync(bool success);
    }

    private sealed class TransactionalTarget(TransactionProbe probe) : ITransactionalTarget
    {
        public IResult Execute(bool success)
        {
            probe.Enlist();
            return success ? new SuccessResult() : new ErrorResult("failed");
        }

        public async Task<IResult> ExecuteAsync(bool success)
        {
            probe.Enlist();
            await Task.Yield();
            return success ? new SuccessResult() : new ErrorResult("failed");
        }
    }

    public interface ICacheTransactionalTarget
    {
        IResult Execute(bool success);
    }

    private sealed class CacheTransactionalTarget(TransactionProbe probe) : ICacheTransactionalTarget
    {
        [CacheRemoveAspect("catalog")]
        [TransactionScopeAspect]
        public IResult Execute(bool success)
        {
            probe.Enlist();
            return success
                ? new CacheInvalidationSuccessResult("done",
                    new CacheInvalidationTarget("product-detail", ArgumentIndex: 0, ArgumentValue: 42))
                : new ErrorResult("failed");
        }
    }

    private sealed class TransactionProbe(List<string>? events = null) : IEnlistmentNotification
    {
        public bool Committed { get; private set; }
        public bool RolledBack { get; private set; }

        public void Enlist() => Transaction.Current!.EnlistVolatile(this, EnlistmentOptions.None);
        public void Prepare(PreparingEnlistment preparingEnlistment) => preparingEnlistment.Prepared();
        public void Commit(Enlistment enlistment) { Committed = true; events?.Add("commit"); enlistment.Done(); }
        public void Rollback(Enlistment enlistment) { RolledBack = true; events?.Add("rollback"); enlistment.Done(); }
        public void InDoubt(Enlistment enlistment) { RolledBack = true; enlistment.Done(); }
    }
}
