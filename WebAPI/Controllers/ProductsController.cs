using Business.Abstract;
using Core.Utilities.Results;
using Entities;
using Entities.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Core.Pagination;
using WebAPI.Utilities;

namespace WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProductsController : ControllerBase
    {
        private IProductService _productService;

        public ProductsController(IProductService productService)
        {
            _productService = productService;
        }

        [HttpPost]
        [EnableRateLimiting("product-create")]
        public async Task<IActionResult> Add(CreateProductRequest request, CancellationToken cancellationToken)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userId, out var createdByUserId))
            {
                return Unauthorized();
            }

            var result = await _productService.CreateAsync(request, createdByUserId, cancellationToken);
            return this.ToHttpResult(result);
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userId, out var deletedByUserId))
            {
                return Unauthorized();
            }

            var result = await _productService.DeleteAsync(id, deletedByUserId, cancellationToken);
            return this.ToHttpResult(result);
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
        {
            var result = await _productService.GetByIdAsync(id, cancellationToken);
            return result.Success
                ? this.ToHttpResult(new SuccessDataResult<ProductResponseDto>(ToResponse(result.Data), result.Message))
                : this.ToHttpResult(result);
        }

        [HttpGet("by-category/{id:int}")]
        public async Task<IActionResult> GetAllByCategoryId(int id, [FromQuery] PageRequest request,
            CancellationToken cancellationToken)
        {
            var result = await _productService.GetPageAsync(request, id, cancellationToken);
            return this.ToHttpResult(result);
        }

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] PageRequest request, CancellationToken cancellationToken)
        {
            var result = await _productService.GetPageAsync(request, cancellationToken: cancellationToken);
            return this.ToHttpResult(result);
        }

        private static ProductResponseDto ToResponse(Product product) => new(
            product.ProductId, product.Name, product.Slug, product.ShortDescription,
            product.Description, product.WebsiteUrl, product.LogoUrl,
            product.CreatedAt, product.UpdatedAt);

        [HttpPut("{id:int}")]
        [EnableRateLimiting("product-update")]
        public async Task<IActionResult> Update(int id, UpdateProductRequest request, CancellationToken cancellationToken)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userId, out var updatedByUserId))
            {
                return Unauthorized();
            }

            var result = await _productService.UpdateAsync(id, request, updatedByUserId, cancellationToken);
            return this.ToHttpResult(result);
        }
    }
}
