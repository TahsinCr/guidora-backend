using Business.BusinessAspect.Autofac;
using Business.Concrete;
using Castle.DynamicProxy;
using Core.Aspects.Caching;
using Core.Aspects.Autofac.Validation;
using Core.Aspects.Performance;
using Core.Aspects.Transaction;
using Core.CrossCuttingConcerns.Caching;
using Core.Utilities.Interceptors.Autofac;
using Core.Utilities.IoC;
using IResult = Core.Utilities.Results.IResult;
using SuccessResult = Core.Utilities.Results.SuccessResult;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using Tests.Core.TestDoubles;
using System.Collections.Generic;
using Business.Constants;

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
            [typeof(SecuredOperationAspect), typeof(PerformanceAspect), typeof(CacheRemoveAspect),
                typeof(TransactionScopeAspect)],
            interceptors.Select(interceptor => interceptor.GetType()).ToArray());
        Assert.Equal(
            [100, 250, 300, 400],
            interceptors.Cast<MethodInterceptionBaseAttribute>().Select(interceptor => interceptor.Priority).ToArray());
    }

    [Fact]
    public void CacheRemovalWrapsTransactionSoItRunsAfterCommit()
    {
        Assert.True(CacheRemoveAspect.InterceptorPriority < TransactionScopeAspect.InterceptorPriority);
    }

    [Theory]
    [InlineData(nameof(ProductManager.CreateAsync), true, 1)]
    [InlineData(nameof(ProductManager.UpdateAsync), true, 1)]
    [InlineData(nameof(ProductManager.DeleteAsync), false, 2)]
    public void ProductWritesResolveAuthorizationValidationCacheAndTransactionInStableNestingOrder(
        string methodName, bool hasValidation, int cacheRemovalCount)
    {
        var services = new ServiceCollection()
            .AddSingleton<ICacheManager>(new CacheManagerStub())
            .AddSingleton<IHttpContextAccessor, HttpContextAccessor>()
            .BuildServiceProvider();
        ServiceTool.SetProvider(services);

        var method = typeof(ProductManager).GetMethod(methodName)!;
        var interceptors = new AspectInterceptorSelector().SelectInterceptors(
            typeof(ProductManager), method, Array.Empty<IInterceptor>());
        var expectedTypes = new List<Type> { typeof(SecuredOperationAspect) };
        var expectedPriorities = new List<int> { SecuredOperationAspect.InterceptorPriority };
        if (hasValidation)
        {
            expectedTypes.Add(typeof(ValidationAspect));
            expectedPriorities.Add(ValidationAspect.InterceptorPriority);
        }
        expectedTypes.AddRange(Enumerable.Repeat(typeof(CacheRemoveAspect), cacheRemovalCount));
        expectedPriorities.AddRange(Enumerable.Repeat(CacheRemoveAspect.InterceptorPriority, cacheRemovalCount));
        expectedTypes.Add(typeof(TransactionScopeAspect));
        expectedPriorities.Add(TransactionScopeAspect.InterceptorPriority);

        Assert.Equal(expectedTypes, interceptors.Select(interceptor => interceptor.GetType()));
        Assert.Equal(expectedPriorities,
            interceptors.Cast<MethodInterceptionBaseAttribute>().Select(interceptor => interceptor.Priority));
    }

    [Fact]
    public void CategoryDeactivationInvalidatesNavigationAndProductListingCaches()
    {
        var method = typeof(CategoryManager).GetMethod(nameof(CategoryManager.DeactivateAsync))!;
        var cacheRemoval = Assert.Single(method.GetCustomAttributesData(), attribute =>
            attribute.AttributeType == typeof(CacheRemoveAspect));
        var profiles = Assert.IsAssignableFrom<IList<CustomAttributeTypedArgument>>(
            cacheRemoval.ConstructorArguments.Single().Value);

        Assert.Equal(
            [CacheProfiles.CatalogNavigation, CacheProfiles.CatalogListing],
            profiles.Select(profile => profile.Value).ToArray());
    }

    [Fact]
    public void ProductUpdateUsesResultDrivenCacheInvalidation()
    {
        var method = typeof(ProductManager).GetMethod(nameof(ProductManager.UpdateAsync))!;
        var cacheRemoval = Assert.Single(method.GetCustomAttributesData(), attribute =>
            attribute.AttributeType == typeof(CacheRemoveAspect));
        var profiles = Assert.IsAssignableFrom<IList<CustomAttributeTypedArgument>>(
            cacheRemoval.ConstructorArguments.Single().Value);

        Assert.Equal([CacheProfiles.CatalogListing], profiles.Select(profile => profile.Value).ToArray());
    }

    [Fact]
    public void BusinessManagerAspectDeclarationsMatchResolvedRuntimeOrder()
    {
        var services = new ServiceCollection()
            .AddSingleton<ICacheManager>(new CacheManagerStub())
            .AddSingleton<IHttpContextAccessor, HttpContextAccessor>()
            .BuildServiceProvider();
        ServiceTool.SetProvider(services);

        var selector = new AspectInterceptorSelector();
        var businessAssembly = typeof(ProductManager).Assembly;
        var checkedMethods = 0;

        foreach (var managerType in businessAssembly.GetTypes()
                     .Where(type => type.Namespace == "Business.Concrete" && !type.IsAbstract))
        {
            foreach (var method in managerType.GetMethods(BindingFlags.Instance | BindingFlags.Public |
                                                          BindingFlags.DeclaredOnly))
            {
                var declaredAspectTypes = method.GetCustomAttributesData()
                    .Where(attribute => typeof(MethodInterceptionBaseAttribute)
                        .IsAssignableFrom(attribute.AttributeType))
                    .Select(attribute => attribute.AttributeType)
                    .ToArray();

                if (declaredAspectTypes.Length == 0)
                {
                    continue;
                }

                IInterceptor[] interceptors = selector.SelectInterceptors(
                    managerType, method, Array.Empty<IInterceptor>());
                var methodAspectTypesInRuntimeOrder = interceptors
                    .Select(interceptor => interceptor.GetType())
                    .Where(type => declaredAspectTypes.Contains(type))
                    .ToArray();

                Assert.Equal(declaredAspectTypes, methodAspectTypesInRuntimeOrder);
                checkedMethods++;
            }
        }

        Assert.True(checkedMethods > 0, "Expected to inspect production Business Manager aspects.");
    }
}

[SecuredOperationAspect("class.permission")]
[PerformanceAspect(1)]
public sealed class AspectSelectionTarget
{
    [SecuredOperationAspect("method.permission")]
    [CacheRemoveAspect("catalog")]
    [TransactionScopeAspect]
    public IResult Write() => new SuccessResult();
}
