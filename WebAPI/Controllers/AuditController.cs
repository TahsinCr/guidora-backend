using System.Threading;
using System.Threading.Tasks;
using System.Security.Claims;
using Business.Abstract;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebAPI.Utilities;

namespace WebAPI.Controllers;

[ApiController]
[Authorize]
[Route("api/audit")]
public sealed class AuditController : ControllerBase
{
    private readonly IAuditRevertService _auditRevertService;

    public AuditController(IAuditRevertService auditRevertService) => _auditRevertService = auditRevertService;

    [HttpPost("{auditLogId:long}/revert")]
    public async Task<IActionResult> Revert(long auditLogId, CancellationToken cancellationToken)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var actorUserId))
            return Unauthorized();

        var result = await _auditRevertService.RevertAsync(auditLogId, actorUserId, cancellationToken);
        return this.ToHttpResult(result);
    }
}
