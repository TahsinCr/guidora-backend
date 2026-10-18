using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Business.Abstract;
using Core.Pagination;
using Microsoft.AspNetCore.Authorization;
using Entities.DTOs;
using Microsoft.AspNetCore.Mvc;
using WebAPI.Utilities;

namespace WebAPI.Controllers;

[ApiController]
[Authorize]
[Route("api/community-verification-reviews")]
public sealed class CommunityVerificationReviewsController(ICommunityVerificationService service) : ControllerBase
{
    [HttpGet("pending")]
    public async Task<IActionResult> GetPending([FromQuery] PageRequest request,
        CancellationToken cancellationToken) =>
        this.ToHttpResult(await service.GetPendingAsync(request, cancellationToken));

    [HttpPost("{productId:int}/approve")]
    public async Task<IActionResult> Approve(int productId, [FromBody] ReviewNoteRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var actorId)) return Unauthorized();
        return this.ToHttpResult(await service.ApproveAsync(productId, actorId, request ?? new ReviewNoteRequest(), cancellationToken));
    }

    [HttpPost("{productId:int}/reject")]
    public async Task<IActionResult> Reject(int productId, [FromBody] ReviewNoteRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var actorId)) return Unauthorized();
        return this.ToHttpResult(await service.RejectAsync(productId, actorId, request ?? new ReviewNoteRequest(), cancellationToken));
    }
}
