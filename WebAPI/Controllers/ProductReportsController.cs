using System.Security.Claims;
using Business.Abstract;
using Entities.DTOs;
using Microsoft.AspNetCore.Mvc;
using System.Threading;
using System.Threading.Tasks;
using Core.Pagination;
using WebAPI.Utilities;

namespace WebAPI.Controllers;

[Route("api/product-reports")]
[ApiController]
public sealed class ProductReportsController : ControllerBase
{
    private readonly IProductReportService _reportService;

    public ProductReportsController(IProductReportService reportService) => _reportService = reportService;

    [HttpGet("open")]
    public async Task<IActionResult> GetOpen([FromQuery] PageRequest request, CancellationToken cancellationToken)
    {
        var result = await _reportService.GetOpenAsync(request, cancellationToken);
        return this.ToHttpResult(result);
    }

    [HttpPost("products/{productId:int}")]
    public async Task<IActionResult> Submit(int productId, ProductReportRequest request,
        CancellationToken cancellationToken)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var reporterId))
            return Unauthorized();
        var result = await _reportService.SubmitAsync(productId, reporterId, request, cancellationToken);
        return this.ToHttpResult(result);
    }

    [HttpPost("{id:int}/resolve")]
    public Task<IActionResult> Resolve(int id, ReviewNoteRequest request, CancellationToken cancellationToken) =>
        ReviewAsync(id, request, resolve: true, cancellationToken);

    [HttpPost("{id:int}/dismiss")]
    public Task<IActionResult> Dismiss(int id, ReviewNoteRequest request, CancellationToken cancellationToken) =>
        ReviewAsync(id, request, resolve: false, cancellationToken);

    private async Task<IActionResult> ReviewAsync(int id, ReviewNoteRequest request, bool resolve,
        CancellationToken cancellationToken)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var reviewerId))
            return Unauthorized();
        var result = resolve
            ? await _reportService.ResolveAsync(id, reviewerId, request, cancellationToken)
            : await _reportService.DismissAsync(id, reviewerId, request, cancellationToken);
        return this.ToHttpResult(result);
    }
}
