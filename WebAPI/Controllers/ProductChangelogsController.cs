using System.Security.Claims;
using Business.Abstract;
using Entities.DTOs;
using Microsoft.AspNetCore.Mvc;
using System.Threading;
using System.Threading.Tasks;

namespace WebAPI.Controllers;

[ApiController]
[Route("api/products/{productId:int}/changelogs")]
public sealed class ProductChangelogsController : ControllerBase
{
    private readonly IProductChangelogService _service;
    public ProductChangelogsController(IProductChangelogService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> Get(int productId, CancellationToken cancellationToken)
    {
        var result = await _service.GetByProductAsync(productId, cancellationToken);
        return result.Success ? Ok(result) : NotFound(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create(int productId, ProductChangelogRequest request,
        CancellationToken cancellationToken)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var actorId)) return Unauthorized();
        var result = await _service.CreateAsync(productId, actorId, request, cancellationToken);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}
