using System.Security.Claims;
using Business.Abstract;
using Entities.DTOs;
using Microsoft.AspNetCore.Mvc;
using System.Threading;
using System.Threading.Tasks;
using Core.Pagination;
using WebAPI.Utilities;

namespace WebAPI.Controllers;

[ApiController]
[Route("api/products/{productId:int}/changelogs")]
public sealed class ProductChangelogsController : ControllerBase
{
    private readonly IProductChangelogService _service;
    public ProductChangelogsController(IProductChangelogService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> Get(int productId, [FromQuery] PageRequest page,
        CancellationToken cancellationToken)
    {
        var result = await _service.GetByProductAsync(productId, page, cancellationToken);
        return this.ToHttpResult(result);
    }

    [HttpGet("{changelogId:int}")]
    public async Task<IActionResult> GetById(int productId, int changelogId, CancellationToken cancellationToken)
    {
        var result = await _service.GetByIdAsync(productId, changelogId, cancellationToken);
        return this.ToHttpResult(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create(int productId, ProductChangelogRequest request,
        CancellationToken cancellationToken)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var actorId)) return Unauthorized();
        var result = await _service.CreateAsync(productId, actorId, request, cancellationToken);
        return this.ToHttpResult(result);
    }
}
