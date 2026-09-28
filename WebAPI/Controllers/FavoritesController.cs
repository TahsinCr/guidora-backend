using System.Security.Claims;
using Business.Abstract;
using Entities.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading;
using System.Threading.Tasks;
using Core.Pagination;
using WebAPI.Utilities;

namespace WebAPI.Controllers;

[ApiController]
[Route("api/favorites")]
public sealed class FavoritesController : ControllerBase
{
    private readonly IFavoriteService _favoriteService;

    public FavoritesController(IFavoriteService favoriteService) => _favoriteService = favoriteService;

    [HttpGet("mine")]
    public async Task<IActionResult> GetMine([FromQuery] PageRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var result = await _favoriteService.GetForUserAsync(userId, request, cancellationToken);
        return this.ToHttpResult(result);
    }

    [HttpPost("products/{productId:int}")]
    [EnableRateLimiting("favorite-mutation")]
    public async Task<IActionResult> Add(int productId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var result = await _favoriteService.AddAsync(productId, userId, cancellationToken);
        return this.ToHttpResult(result);
    }

    [HttpDelete("products/{productId:int}")]
    [EnableRateLimiting("favorite-mutation")]
    public async Task<IActionResult> Remove(int productId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var result = await _favoriteService.RemoveAsync(productId, userId, cancellationToken);
        return this.ToHttpResult(result);
    }

    private bool TryGetUserId(out int userId) =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
}
