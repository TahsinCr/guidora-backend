using System.Security.Claims;
using Business.Abstract;
using Entities.DTOs;
using Microsoft.AspNetCore.Mvc;
using System.Threading;
using System.Threading.Tasks;
using WebAPI.Utilities;

namespace WebAPI.Controllers;

[ApiController]
[Route("api/products/{productId:int}/contributors")]
public sealed class ProductContributorsController : ControllerBase
{
    private readonly IProductContributorService _service;
    public ProductContributorsController(IProductContributorService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> Get(int productId, CancellationToken cancellationToken)
    {
        var result = await _service.GetByProductAsync(productId, cancellationToken);
        return this.ToHttpResult(result);
    }

    [HttpPost]
    public async Task<IActionResult> Add(int productId, ProductContributorRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryUserId(out var actorId)) return Unauthorized();
        var result = await _service.AddAsync(productId, actorId, request, cancellationToken);
        return this.ToHttpResult(result);
    }

    [HttpDelete("{userId:int}")]
    public async Task<IActionResult> Remove(int productId, int userId, CancellationToken cancellationToken)
    {
        if (!TryUserId(out var actorId)) return Unauthorized();
        var result = await _service.RemoveAsync(productId, userId, actorId, cancellationToken);
        return this.ToHttpResult(result);
    }

    private bool TryUserId(out int userId) => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
}
