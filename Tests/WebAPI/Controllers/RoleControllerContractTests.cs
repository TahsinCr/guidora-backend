using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using WebAPI.Controllers;

namespace Tests.WebAPI.Controllers;

public sealed class RoleControllerContractTests
{
    [Fact]
    public void ControllerUsesTheRolesRouteAndExposesPermissionCatalog()
    {
        Assert.Equal("api/roles", typeof(RolesController).GetCustomAttribute<RouteAttribute>()?.Template);
        Assert.Equal("permissions", typeof(RolesController).GetMethod(nameof(RolesController.GetOperationClaims))!
            .GetCustomAttribute<HttpGetAttribute>()?.Template);
    }

    [Theory]
    [InlineData(nameof(RolesController.Create), typeof(HttpPostAttribute), null)]
    [InlineData(nameof(RolesController.Update), typeof(HttpPutAttribute), "{roleId:int}")]
    [InlineData(nameof(RolesController.Deactivate), typeof(HttpDeleteAttribute), "{roleId:int}")]
    [InlineData(nameof(RolesController.Activate), typeof(HttpPutAttribute), "{roleId:int}/activate")]
    [InlineData(nameof(RolesController.GrantUserPermission), typeof(HttpPutAttribute), "users/{userId:int}/permissions/{operationClaimId:int}")]
    [InlineData(nameof(RolesController.RevokeUserPermission), typeof(HttpDeleteAttribute), "users/{userId:int}/permissions/{operationClaimId:int}")]
    public void ManagementCommandsExposeTheirExpectedRoutes(string methodName, Type attributeType, string? template)
    {
        var method = typeof(RolesController).GetMethod(methodName)!;
        var route = method.GetCustomAttributes(attributeType).Single();
        var templateProperty = attributeType.GetProperty("Template");
        Assert.Equal(template, templateProperty?.GetValue(route));
    }
}
