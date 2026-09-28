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
[Route("api")]
public sealed class ProductCommentsController : ControllerBase
{
    private readonly ICommentService _commentService;

    public ProductCommentsController(ICommentService commentService) => _commentService = commentService;

    [HttpGet("products/{productId:int}/comments")]
    public async Task<IActionResult> GetVisible(int productId, [FromQuery] PageRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _commentService.GetVisibleByProductAsync(productId, request, cancellationToken);
        return this.ToHttpResult(result);
    }

    [HttpPost("products/{productId:int}/comments")]
    [EnableRateLimiting("comment-create")]
    public async Task<IActionResult> Create(int productId, CommentRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var result = await _commentService.CreateAsync(productId, userId, request, cancellationToken);
        return this.ToHttpResult(result);
    }

    [HttpDelete("comments/{commentId:int}")]
    public async Task<IActionResult> Delete(int commentId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var result = await _commentService.DeleteAsync(commentId, userId, cancellationToken);
        return this.ToHttpResult(result);
    }

    [HttpPatch("comments/{commentId:int}/visibility")]
    public async Task<IActionResult> SetVisibility(int commentId, CommentVisibilityRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var result = await _commentService.SetVisibilityAsync(commentId, userId, request.IsVisible, cancellationToken);
        return this.ToHttpResult(result);
    }

    [HttpPost("comments/{commentId:int}/block")]
    public async Task<IActionResult> Block(int commentId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var result = await _commentService.BlockAsync(commentId, userId, cancellationToken);
        return this.ToHttpResult(result);
    }

    private bool TryGetUserId(out int userId) =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
}
