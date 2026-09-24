using Business.Abstract;
using Entities.DTOs;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using WebAPI.Utilities;

namespace WebAPI.Controllers;

[ApiController]
[Route("api/tags")]
public sealed class TagsController(ITagService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetActive([FromQuery] int? tagCategoryId, CancellationToken cancellationToken)
    {
        var result = await service.GetActiveAsync(tagCategoryId, cancellationToken);
        return this.ToHttpResult(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create(TagRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId)) return Unauthorized();
        var result = await service.CreateAsync(request, actorId, cancellationToken);
        return this.ToHttpResult(result);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, TagRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId)) return Unauthorized();
        var result = await service.UpdateAsync(id, request, actorId, cancellationToken);
        return this.ToHttpResult(result);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Deactivate(int id, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId)) return Unauthorized();
        var result = await service.DeactivateAsync(id, actorId, cancellationToken);
        return this.ToHttpResult(result);
    }

    private bool TryGetActor(out int id) =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out id);
}
