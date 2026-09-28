using System.Security.Claims;
using Business.Abstract;
using Entities.DTOs;
using Microsoft.AspNetCore.Mvc;
using System.Threading;
using System.Threading.Tasks;
using Core.Pagination;
using WebAPI.Utilities;

namespace WebAPI.Controllers;

[Route("api/product-edit-proposals")]
[ApiController]
public sealed class ProductEditProposalsController : ControllerBase
{
    private readonly IProductEditProposalService _proposalService;

    public ProductEditProposalsController(IProductEditProposalService proposalService) =>
        _proposalService = proposalService;

    [HttpGet("pending")]
    public async Task<IActionResult> GetPending([FromQuery] PageRequest request, CancellationToken cancellationToken)
    {
        var result = await _proposalService.GetPendingAsync(request, cancellationToken);
        return this.ToHttpResult(result);
    }

    [HttpPost("{id:int}/accept")]
    public Task<IActionResult> Accept(int id, ReviewNoteRequest request, CancellationToken cancellationToken) =>
        ReviewAsync(id, request, accept: true, cancellationToken);

    [HttpPost("{id:int}/reject")]
    public Task<IActionResult> Reject(int id, ReviewNoteRequest request, CancellationToken cancellationToken) =>
        ReviewAsync(id, request, accept: false, cancellationToken);

    private async Task<IActionResult> ReviewAsync(int id, ReviewNoteRequest request, bool accept,
        CancellationToken cancellationToken)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var reviewerId))
            return Unauthorized();
        var result = accept
            ? await _proposalService.AcceptAsync(id, reviewerId, request, cancellationToken)
            : await _proposalService.RejectAsync(id, reviewerId, request, cancellationToken);
        return this.ToHttpResult(result);
    }
}
