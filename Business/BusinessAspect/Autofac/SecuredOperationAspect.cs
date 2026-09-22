using Business.Constants;
using Castle.DynamicProxy;
using Core.Utilities.Interceptors.Autofac;
using Microsoft.AspNetCore.Http;
using System;
using Core.Extensions;
using Core.Utilities.IoC;
using Microsoft.Extensions.DependencyInjection;
using Core.Utilities.Security.Authorization;

namespace Business.BusinessAspect.Autofac
{
    public class SecuredOperationAspect : MethodInterception
    {
        private readonly string[] _roles;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public SecuredOperationAspect(params string[] roles)
        {
            _roles = roles;
            _httpContextAccessor = ServiceTool.ServiceProvider.GetRequiredService<IHttpContextAccessor>();

        }

        protected override void OnBefore(IInvocation invocation)
        {
            var roleClaims = _httpContextAccessor.HttpContext?.User.ClaimRoles() ?? new System.Collections.Generic.List<string>();
            foreach (var role in _roles)
            {
                if (roleClaims.Contains(role))
                {
                    return;
                }
            }
            throw new AuthorizationDeniedException(Messages.AuthorizationDenied);
        }
    }
}
