using Entities;
using Entities.DTOs;
using Microsoft.AspNetCore.Mvc;
using System.Reflection;
using WebAPI.Controllers;

namespace Tests.WebAPI.Controllers;

public sealed class CategoryControllerContractTests
{
    [Fact]
    public void CategoryRoutesUseResourceOrientedHttpVerbsAndWriteDto()
    {
        var controller = typeof(CategorysController);
        Assert.Equal("api/categories", controller.GetCustomAttribute<RouteAttribute>()?.Template);

        var create = controller.GetMethod(nameof(CategorysController.Add))!;
        var update = controller.GetMethod(nameof(CategorysController.Update))!;
        var delete = controller.GetMethod(nameof(CategorysController.Delete))!;
        var managementRead = controller.GetMethod(nameof(CategorysController.GetAllForManagement))!;

        Assert.IsType<HttpPostAttribute>(create.GetCustomAttribute<HttpPostAttribute>());
        Assert.IsType<HttpPutAttribute>(update.GetCustomAttribute<HttpPutAttribute>());
        Assert.IsType<HttpDeleteAttribute>(delete.GetCustomAttribute<HttpDeleteAttribute>());
        Assert.Equal("management", managementRead.GetCustomAttribute<HttpGetAttribute>()?.Template);
        Assert.Equal(typeof(CategoryRequest), create.GetParameters()[0].ParameterType);
        Assert.Equal(typeof(int), update.GetParameters()[0].ParameterType);
        Assert.Equal(typeof(CategoryRequest), update.GetParameters()[1].ParameterType);
        Assert.DoesNotContain(create.GetParameters(), parameter => parameter.ParameterType == typeof(Category));
        Assert.DoesNotContain(update.GetParameters(), parameter => parameter.ParameterType == typeof(Category));
        Assert.DoesNotContain(delete.GetParameters(), parameter => parameter.ParameterType == typeof(Category));
    }
}
