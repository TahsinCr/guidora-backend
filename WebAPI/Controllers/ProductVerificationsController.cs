using System.Security.Claims;
using System;
using Business.Abstract;
using Entities.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using WebAPI.Utilities;

namespace WebAPI.Controllers;

[Route("api/products/{productId:int}/verification")]
[ApiController]
public sealed class ProductVerificationsController : ControllerBase
{
    private readonly IProductVerificationService _verificationService;
    private readonly ICommunityVerificationService _communityVerificationService;
    private readonly string _ipSignalKey;

    public ProductVerificationsController(IProductVerificationService verificationService,
        ICommunityVerificationService communityVerificationService, IConfiguration configuration)
    {
        _verificationService = verificationService;
        _communityVerificationService = communityVerificationService;
        _ipSignalKey = configuration["CommunityVerification:SignalHashKey"] ?? string.Empty;
    }

    [HttpGet]
    public async Task<IActionResult> GetSummary(int productId, CancellationToken cancellationToken)
    {
        var currentUserId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsedUserId)
            ? parsedUserId : (int?)null;
        var result = await _verificationService.GetSummaryAsync(productId, currentUserId, cancellationToken);
        return this.ToHttpResult(result);
    }

    [HttpPost("admin")]
    public async Task<IActionResult> VerifyAsAdmin(int productId, [FromBody] ReviewNoteRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var actorId))
            return Unauthorized();
        var result = await _verificationService.VerifyAsAdminAsync(productId, actorId, request ?? new ReviewNoteRequest(), cancellationToken);
        return this.ToHttpResult(result);
    }

    [HttpDelete("admin")]
    public async Task<IActionResult> RevokeAdminVerification(int productId, [FromBody] ReviewNoteRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var actorId))
            return Unauthorized();
        var result = await _verificationService.RevokeAdminVerificationAsync(productId, actorId, request ?? new ReviewNoteRequest(), cancellationToken);
        return this.ToHttpResult(result);
    }

    [HttpPost("community-votes")]
    [EnableRateLimiting("community-vote")]
    public async Task<IActionResult> VoteCommunity(int productId, CommunityVerificationVoteRequest request,
        CancellationToken cancellationToken)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            return Unauthorized();
        var address = HttpContext.Connection.RemoteIpAddress;
        var canonicalAddress = address?.IsIPv4MappedToIPv6 == true ? address.MapToIPv4() : address;
        var ipHash = canonicalAddress is null || string.IsNullOrEmpty(_ipSignalKey) ? null :
            Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(_ipSignalKey),
                Encoding.UTF8.GetBytes("ip:" + canonicalAddress)));
        var userAgent = Request.Headers.UserAgent.ToString();
        var clientHash = string.IsNullOrWhiteSpace(userAgent) || string.IsNullOrEmpty(_ipSignalKey) ? null :
            Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(_ipSignalKey),
                Encoding.UTF8.GetBytes("client:" + userAgent[..Math.Min(userAgent.Length, 512)])));
        var result = await _communityVerificationService.VoteAsync(productId, userId, request,
            cancellationToken, ipHash, clientHash);
        return this.ToHttpResult(result);
    }


    [HttpDelete("community")]
    public async Task<IActionResult> RevokeCommunity(int productId, [FromBody] ReviewNoteRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var actorId))
            return Unauthorized();
        var result = await _communityVerificationService.RevokeAsync(productId, actorId, request ?? new ReviewNoteRequest(), cancellationToken);
        return this.ToHttpResult(result);
    }
}
