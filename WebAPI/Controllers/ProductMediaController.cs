using System.Security.Claims;
using Business.Abstract;
using Entities.DTOs;
using Microsoft.AspNetCore.Mvc;
using System.Threading;
using System.Threading.Tasks;
using WebAPI.Utilities;

namespace WebAPI.Controllers;

[ApiController]
[Route("api/products/{productId:int}/media")]
public sealed class ProductMediaController : ControllerBase
{
    private readonly IProductMediaService _service;
    public ProductMediaController(IProductMediaService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> Get(int productId, CancellationToken cancellationToken)
    {
        var result = await _service.GetByProductAsync(productId, cancellationToken);
        return this.ToHttpResult(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create(int productId, ProductMediaRequest request, CancellationToken cancellationToken)
    {
        if (!TryUserId(out var actorId)) return Unauthorized();
        var result = await _service.CreateAsync(productId, actorId, request, cancellationToken);
        return this.ToHttpResult(result);
    }

    [HttpPut("{mediaId:int}")]
    public async Task<IActionResult> Update(int productId, int mediaId, ProductMediaRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryUserId(out var actorId)) return Unauthorized();
        var result = await _service.UpdateAsync(productId, mediaId, actorId, request, cancellationToken);
        return this.ToHttpResult(result);
    }

    [HttpDelete("{mediaId:int}")]
    public async Task<IActionResult> Delete(int productId, int mediaId, CancellationToken cancellationToken)
    {
        if (!TryUserId(out var actorId)) return Unauthorized();
        var result = await _service.DeleteAsync(productId, mediaId, actorId, cancellationToken);
        return this.ToHttpResult(result);
    }

    private bool TryUserId(out int userId) => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
}
