using System.Security.Claims;
using Business.Abstract;
using Entities.DTOs;
using Microsoft.AspNetCore.Mvc;
using System.Threading;
using System.Threading.Tasks;
using Core.Pagination;
using WebAPI.Utilities;
using Microsoft.AspNetCore.RateLimiting;

namespace WebAPI.Controllers;

[ApiController]
[Route("api/products/{productId:int}/documents")]
public sealed class ProductDocumentsController : ControllerBase
{
    private readonly IProductDocumentService _service;
    public ProductDocumentsController(IProductDocumentService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> Get(int productId, [FromQuery] PageRequest page,
        CancellationToken cancellationToken)
    {
        var result = await _service.GetByProductAsync(productId, page, cancellationToken);
        return this.ToHttpResult(result);
    }

    [HttpGet("{documentId:int}")]
    public async Task<IActionResult> GetById(int productId, int documentId, CancellationToken cancellationToken)
    {
        var result = await _service.GetByIdAsync(productId, documentId, cancellationToken);
        return this.ToHttpResult(result);
    }

    [HttpPost]
    [EnableRateLimiting("product-content-mutation")]
    public async Task<IActionResult> Create(int productId, ProductDocumentRequest request, CancellationToken cancellationToken)
    {
        if (!TryUserId(out var actorId)) return Unauthorized();
        var result = await _service.CreateAsync(productId, actorId, request, cancellationToken);
        return this.ToHttpResult(result);
    }

    [HttpPut("{documentId:int}")]
    [EnableRateLimiting("product-content-mutation")]
    public async Task<IActionResult> Update(int productId, int documentId, ProductDocumentRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryUserId(out var actorId)) return Unauthorized();
        var result = await _service.UpdateAsync(productId, documentId, actorId, request, cancellationToken);
        return this.ToHttpResult(result);
    }

    [HttpDelete("{documentId:int}")]
    [EnableRateLimiting("product-content-mutation")]
    public async Task<IActionResult> Delete(int productId, int documentId, CancellationToken cancellationToken)
    {
        if (!TryUserId(out var actorId)) return Unauthorized();
        var result = await _service.DeleteAsync(productId, documentId, actorId, cancellationToken);
        return this.ToHttpResult(result);
    }

    private bool TryUserId(out int userId) => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
}
