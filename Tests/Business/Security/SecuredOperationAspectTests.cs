using System.Security.Claims;
using Business.BusinessAspect.Autofac;
using Castle.DynamicProxy;
using Core.Utilities.IoC;
using Core.Utilities.Security.Authorization;
using IResult = Core.Utilities.Results.IResult;
using SuccessResult = Core.Utilities.Results.SuccessResult;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Tests.Business.Security;

public sealed class SecuredOperationAspectTests
{
    [Fact]
    public void AcceptsEffectiveOperationClaimRegardlessOfRoleOrDirectClaimSource()
    {
        ConfigureUser("PRODUCT.CREATE");
        var proxy = CreateProxy(new SecuredOperationAspect("Product.Create"));

        Assert.True(proxy.Create().Success);
    }

    [Fact]
    public void ReturnsAuthorizationDeniedWhenAuthenticatedUserLacksOperationClaim()
    {
        ConfigureUser("catalog.read");
        var proxy = CreateProxy(new SecuredOperationAspect("Product.Create"));

        Assert.Throws<AuthorizationDeniedException>(() => proxy.Create());
    }

    [Fact]
    public void ReturnsAuthenticationRequiredWhenRequestIsAnonymous()
    {
        ConfigureAnonymous();
        var proxy = CreateProxy(new SecuredOperationAspect("Product.Create"));

        Assert.Throws<AuthenticationRequiredException>(() => proxy.Create());
    }

    [Fact]
    public void UsesDefaultConventionWhenNoExplicitClaimWasProvided()
    {
        ConfigureUser("secured-target.read-product");
        var proxy = CreateProxy(new SecuredOperationAspect());

        Assert.True(proxy.ReadProduct().Success);
    }

    private static ISecuredTarget CreateProxy(SecuredOperationAspect aspect) =>
        new ProxyGenerator().CreateInterfaceProxyWithTarget<ISecuredTarget>(new SecuredTarget(), aspect);

    private static void ConfigureUser(string operationClaim)
    {
        var accessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimTypes.Role, operationClaim)], authenticationType: "test"))
            }
        };
        ServiceTool.SetProvider(new ServiceCollection().AddSingleton<IHttpContextAccessor>(accessor).BuildServiceProvider());
    }

    private static void ConfigureAnonymous()
    {
        var accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext() };
        ServiceTool.SetProvider(new ServiceCollection().AddSingleton<IHttpContextAccessor>(accessor).BuildServiceProvider());
    }

    public interface ISecuredTarget
    {
        IResult Create();
        IResult ReadProduct();
    }

    private sealed class SecuredTarget : ISecuredTarget
    {
        public IResult Create() => new SuccessResult();
        public IResult ReadProduct() => new SuccessResult();
    }
}
