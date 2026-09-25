using System.Security.Claims;
using Business.Abstract;
using Entities.DTOs;
using Microsoft.AspNetCore.Mvc;
using System.Threading;
using System.Threading.Tasks;
using WebAPI.Utilities;

namespace WebAPI.Controllers;

[Route("api/products/{productId:int}/verification")]
[ApiController]
public sealed class ProductVerificationsController : ControllerBase
{
    private readonly IProductVerificationService _verificationService;
    private readonly ICommunityVerificationService _communityVerificationService;

    public ProductVerificationsController(IProductVerificationService verificationService,
        ICommunityVerificationService communityVerificationService)
    {
        _verificationService = verificationService;
        _communityVerificationService = communityVerificationService;
    }

    [HttpPost("admin")]
    public async Task<IActionResult> VerifyAsAdmin(int productId, ReviewNoteRequest request,
        CancellationToken cancellationToken)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var actorId))
            return Unauthorized();
        var result = await _verificationService.VerifyAsAdminAsync(productId, actorId, request, cancellationToken);
        return this.ToHttpResult(result);
    }

    [HttpDelete("admin")]
    public async Task<IActionResult> RevokeAdminVerification(int productId, [FromBody] ReviewNoteRequest request,
        CancellationToken cancellationToken)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var actorId))
            return Unauthorized();
        var result = await _verificationService.RevokeAdminVerificationAsync(productId, actorId, request, cancellationToken);
        return this.ToHttpResult(result);
    }

    [HttpPost("community-votes")]
    public async Task<IActionResult> VoteCommunity(int productId, CommunityVerificationVoteRequest request,
        CancellationToken cancellationToken)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            return Unauthorized();
        var result = await _communityVerificationService.VoteAsync(productId, userId, request, cancellationToken);
        return this.ToHttpResult(result);
    }

    [HttpDelete("community")]
    public async Task<IActionResult> RevokeCommunity(int productId, [FromBody] ReviewNoteRequest request,
        CancellationToken cancellationToken)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var actorId))
            return Unauthorized();
        var result = await _communityVerificationService.RevokeAsync(productId, actorId, request, cancellationToken);
        return this.ToHttpResult(result);
    }
}
