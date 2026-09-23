using System.Security.Claims;
using Business.Abstract;
using Entities.DTOs;
using Microsoft.AspNetCore.Mvc;
using System.Threading;
using System.Threading.Tasks;

namespace WebAPI.Controllers;

[ApiController]
[Route("api/products/{productId:int}/documents")]
public sealed class ProductDocumentsController : ControllerBase
{
    private readonly IProductDocumentService _service;
    public ProductDocumentsController(IProductDocumentService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> Get(int productId, CancellationToken cancellationToken)
    {
        var result = await _service.GetByProductAsync(productId, cancellationToken);
        return result.Success ? Ok(result) : NotFound(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create(int productId, ProductDocumentRequest request, CancellationToken cancellationToken)
    {
        if (!TryUserId(out var actorId)) return Unauthorized();
        var result = await _service.CreateAsync(productId, actorId, request, cancellationToken);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPut("{documentId:int}")]
    public async Task<IActionResult> Update(int productId, int documentId, ProductDocumentRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryUserId(out var actorId)) return Unauthorized();
        var result = await _service.UpdateAsync(productId, documentId, actorId, request, cancellationToken);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpDelete("{documentId:int}")]
    public async Task<IActionResult> Delete(int productId, int documentId, CancellationToken cancellationToken)
    {
        if (!TryUserId(out var actorId)) return Unauthorized();
        var result = await _service.DeleteAsync(productId, documentId, actorId, cancellationToken);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    private bool TryUserId(out int userId) => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
}
