using Business.Constants;
using Castle.DynamicProxy;
using Core.Utilities.Interceptors.Autofac;
using Microsoft.AspNetCore.Http;
using System;
using Core.Extensions;
using Core.Utilities.IoC;
using Microsoft.Extensions.DependencyInjection;
using Core.Utilities.Security.Authorization;
using System.Globalization;
using System.Text;
using System.Reflection;
using System.Linq;

namespace Business.BusinessAspect.Autofac
{
    public class SecuredOperationAspect : MethodInterception
    {
        public const int InterceptorPriority = 100;
        private readonly string[] _operationClaims;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public SecuredOperationAspect(params string[] operationClaims)
        {
            Priority = InterceptorPriority;
            _operationClaims = operationClaims;
            _httpContextAccessor = ServiceTool.ServiceProvider.GetRequiredService<IHttpContextAccessor>();

        }

        protected override void OnBefore(IInvocation invocation)
        {
            var user = _httpContextAccessor.HttpContext?.User;
            if (user?.Identity?.IsAuthenticated != true)
            {
                throw new AuthenticationRequiredException("Authentication is required.");
            }

            var operationClaims = user.ClaimRoles();
            var requiredClaims = _operationClaims.Length > 0
                ? _operationClaims
                : [ResolveOperationClaim(invocation)];

            foreach (var operationClaim in requiredClaims)
            {
                if (operationClaims.Contains(operationClaim, StringComparer.OrdinalIgnoreCase))
                {
                    return;
                }
            }
            throw new AuthorizationDeniedException(Messages.AuthorizationDenied);
        }

        public static string ResolveOperationClaim(IInvocation invocation)
        {
            var method = invocation.Method;
            return ResolveOperationClaim(invocation.InvocationTarget?.GetType() ?? invocation.TargetType ?? method.DeclaringType!, method);
        }

        public static string ResolveOperationClaim(Type implementationType, MethodInfo method)
        {
            ArgumentNullException.ThrowIfNull(implementationType);
            ArgumentNullException.ThrowIfNull(method);

            var className = ToKebabCase(implementationType.Name);
            var methodName = ToKebabCase(method.Name);
            return $"{className}.{methodName}";
        }

        private static string ToKebabCase(string value)
        {
            var result = new StringBuilder(value.Length + 8);
            for (var index = 0; index < value.Length; index++)
            {
                var current = value[index];
                if (char.IsUpper(current) && index > 0 &&
                    (char.IsLower(value[index - 1]) || (index + 1 < value.Length && char.IsLower(value[index + 1]))))
                {
                    result.Append('-');
                }

                result.Append(char.ToLower(current, CultureInfo.InvariantCulture));
            }

            return result.ToString();
        }
    }
}
