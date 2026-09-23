using Entities;
using Entities.DTOs;
using Microsoft.AspNetCore.Mvc;
using System.Reflection;
using WebAPI.Controllers;

namespace Tests.WebAPI.Controllers;

public sealed class ProductControllerContractTests
{
    [Fact]
    public void ProductRoutesUseResourceOrientedVerbsAndCommandDtos()
    {
        var controller = typeof(ProductsController);
        Assert.Equal("api/[controller]", controller.GetCustomAttribute<RouteAttribute>()?.Template);

        var create = controller.GetMethod(nameof(ProductsController.Add))!;
        var update = controller.GetMethod(nameof(ProductsController.Update))!;
        var delete = controller.GetMethod(nameof(ProductsController.Delete))!;
        Assert.IsType<HttpPostAttribute>(create.GetCustomAttribute<HttpPostAttribute>());
        Assert.IsType<HttpPutAttribute>(update.GetCustomAttribute<HttpPutAttribute>());
        Assert.IsType<HttpDeleteAttribute>(delete.GetCustomAttribute<HttpDeleteAttribute>());
        Assert.Equal("{id:int}", update.GetCustomAttribute<HttpPutAttribute>()?.Template);
        Assert.Equal(typeof(CreateProductRequest), create.GetParameters()[0].ParameterType);
        Assert.Equal(typeof(UpdateProductRequest), update.GetParameters()[1].ParameterType);
        Assert.DoesNotContain(create.GetParameters(), parameter => parameter.ParameterType == typeof(Product));
        Assert.DoesNotContain(update.GetParameters(), parameter => parameter.ParameterType == typeof(Product));
        Assert.DoesNotContain(delete.GetParameters(), parameter => parameter.ParameterType == typeof(Product));
    }
}
