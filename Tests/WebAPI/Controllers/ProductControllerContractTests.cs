using Entities;
using Entities.DTOs;
using Business.Abstract;
using Core.Utilities.Results;
using Microsoft.AspNetCore.Mvc;
using System.Reflection;
using System.Text.Json;
using Core.Pagination;
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
        var getAll = controller.GetMethod(nameof(ProductsController.GetAll))!;
        var getById = controller.GetMethod(nameof(ProductsController.GetById))!;
        Assert.IsType<HttpPostAttribute>(create.GetCustomAttribute<HttpPostAttribute>());
        Assert.IsType<HttpPutAttribute>(update.GetCustomAttribute<HttpPutAttribute>());
        Assert.IsType<HttpDeleteAttribute>(delete.GetCustomAttribute<HttpDeleteAttribute>());
        Assert.IsType<HttpGetAttribute>(getAll.GetCustomAttribute<HttpGetAttribute>());
        Assert.Equal("{id:int}", getById.GetCustomAttribute<HttpGetAttribute>()?.Template);
        Assert.Equal("{id:int}", update.GetCustomAttribute<HttpPutAttribute>()?.Template);
        Assert.Equal(typeof(CreateProductRequest), create.GetParameters()[0].ParameterType);
        Assert.Equal(typeof(UpdateProductRequest), update.GetParameters()[1].ParameterType);
        Assert.DoesNotContain(create.GetParameters(), parameter => parameter.ParameterType == typeof(Product));
        Assert.DoesNotContain(update.GetParameters(), parameter => parameter.ParameterType == typeof(Product));
        Assert.DoesNotContain(delete.GetParameters(), parameter => parameter.ParameterType == typeof(Product));
    }

    [Fact]
    public void PublicProductResponseDoesNotExposePersistenceFields()
    {
        var payload = JsonSerializer.Serialize(new ProductResponseDto(1, "Name", "name", "Short", "Details",
            "https://example.test", "https://example.test/logo", DateTime.UtcNow, null));

        Assert.DoesNotContain("Version", payload);
        Assert.DoesNotContain("ProductStatusId", payload);
        Assert.DoesNotContain("CreatedByUserId", payload);
    }

    [Fact]
    public async Task ProductDetailMapsEntityToPublicResponse()
    {
        var controller = new ProductsController(new ProductServiceStub());

        var response = Assert.IsType<OkObjectResult>(await controller.GetById(5, default));
        var result = Assert.IsAssignableFrom<IDataResult<ProductResponseDto>>(response.Value);
        Assert.Equal(5, result.Data.ProductId);
        Assert.Equal("Catalog item", result.Data.Name);
        Assert.NotEqual(typeof(Product), result.Data.GetType());
    }

    private sealed class ProductServiceStub : IProductService
    {
        public Task<IDataResult<Product>> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
            Task.FromResult<IDataResult<Product>>(new SuccessDataResult<Product>(new Product
            {
                ProductId = id, Name = "Catalog item", Version = 42, CreatedByUserId = 9
            }));
        public Task<IDataResult<PagedResult<ProductListItemDto>>> GetPageAsync(PageRequest request, int? categoryId = null,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IResult> CreateAsync(CreateProductRequest request, int createdByUserId,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IResult> UpdateAsync(int productId, UpdateProductRequest request, int updatedByUserId,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IResult> DeleteAsync(int productId, int deletedByUserId,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
