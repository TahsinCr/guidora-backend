using Business.Abstract;
using Entities.DTOs;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using WebAPI.Utilities;

namespace WebAPI.Controllers;

[ApiController]
[Route("api/tag-categories")]
public sealed class TagCategoriesController(ITagCategoryService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetActive(CancellationToken cancellationToken)
    {
        var result = await service.GetActiveAsync(cancellationToken);
        return this.ToHttpResult(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create(TagCategoryRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorId)) return Unauthorized();
        var result = await service.CreateAsync(request, actorId, cancellationToken);
        return this.ToHttpResult(result);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, TagCategoryRequest request, CancellationToken cancellationToken)
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
