using System.Security.Claims;
using Business.Abstract;
using Entities.DTOs;
using Microsoft.AspNetCore.Mvc;
using System.Threading;
using System.Threading.Tasks;

namespace WebAPI.Controllers;

[Route("api/products/{productId:int}/verification")]
[ApiController]
public sealed class ProductVerificationsController : ControllerBase
{
    private readonly IProductVerificationService _verificationService;

    public ProductVerificationsController(IProductVerificationService verificationService) =>
        _verificationService = verificationService;

    [HttpPost("admin")]
    public async Task<IActionResult> VerifyAsAdmin(int productId, ReviewNoteRequest request,
        CancellationToken cancellationToken)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var actorId))
            return Unauthorized();
        var result = await _verificationService.VerifyAsAdminAsync(productId, actorId, request, cancellationToken);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpDelete("admin")]
    public async Task<IActionResult> RevokeAdminVerification(int productId, [FromBody] ReviewNoteRequest request,
        CancellationToken cancellationToken)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var actorId))
            return Unauthorized();
        var result = await _verificationService.RevokeAdminVerificationAsync(productId, actorId, request, cancellationToken);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}
