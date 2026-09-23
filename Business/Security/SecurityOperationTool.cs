using System.Reflection;
using Business.BusinessAspect.Autofac;
using Core.DataAccess;
using Core.Entities.Concrete;
using DataAccess.Abstract;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Business.Security;

public static class SecurityOperationTool
{
    public static IReadOnlyCollection<string> DiscoverOperationClaims(Assembly? assembly = null)
    {
        assembly ??= typeof(SecurityOperationTool).Assembly;

        return assembly.GetTypes()
            .Where(type => type.IsClass && !type.IsAbstract)
            .SelectMany(type => type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .SelectMany(method => GetEffectiveClaims(type, method)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(claim => claim, StringComparer.Ordinal)
            .ToArray();
    }

    public static int SyncOperationClaims(IOperationClaimDal operationClaimDal, IUnitOfWork unitOfWork,
        Assembly? assembly = null)
    {
        ArgumentNullException.ThrowIfNull(operationClaimDal);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        var existingNames = operationClaimDal.GetAll()
            .Select(claim => claim.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var missingNames = DiscoverOperationClaims(assembly)
            .Where(name => existingNames.Add(name))
            .ToArray();

        foreach (var name in missingNames)
        {
            operationClaimDal.Add(new OperationClaim { Name = name });
        }

        if (missingNames.Length > 0)
        {
            unitOfWork.SaveChanges();
        }

        return missingNames.Length;
    }

    private static IEnumerable<string> GetEffectiveClaims(Type implementationType, MethodInfo method)
    {
        var methodAttributes = GetSecurityAttributes(method.GetCustomAttributesData());
        var attributes = methodAttributes.Count > 0
            ? methodAttributes
            : GetInheritedSecurityAttributes(implementationType);

        foreach (var attribute in attributes)
        {
            var explicitClaims = GetStringArguments(attribute);
            if (explicitClaims.Length > 0)
            {
                foreach (var claim in explicitClaims.Where(claim => !string.IsNullOrWhiteSpace(claim)))
                {
                    yield return claim.Trim();
                }
            }
            else
            {
                yield return SecuredOperationAspect.ResolveOperationClaim(implementationType, method);
            }
        }
    }

    private static List<CustomAttributeData> GetInheritedSecurityAttributes(Type type)
    {
        var attributes = new List<CustomAttributeData>();
        for (var current = type; current is not null; current = current.BaseType)
        {
            attributes.AddRange(GetSecurityAttributes(CustomAttributeData.GetCustomAttributes(current)));
        }

        return attributes;
    }

    private static List<CustomAttributeData> GetSecurityAttributes(IEnumerable<CustomAttributeData> attributes) =>
        attributes.Where(attribute => attribute.AttributeType == typeof(SecuredOperationAspect)).ToList();

    private static string[] GetStringArguments(CustomAttributeData attribute)
    {
        if (attribute.ConstructorArguments.Count == 0)
        {
            return Array.Empty<string>();
        }

        var argument = attribute.ConstructorArguments[0];
        if (argument.Value is not IEnumerable<CustomAttributeTypedArgument> values)
        {
            return argument.Value is string single ? [single] : Array.Empty<string>();
        }

        return values.Select(value => value.Value as string)
            .Where(value => value is not null)
            .Cast<string>()
            .ToArray();
    }
}
