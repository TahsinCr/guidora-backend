using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Business.Abstract;
using Entities.DTOs;
using Microsoft.AspNetCore.Mvc;
using WebAPI.Utilities;

namespace WebAPI.Controllers;

[ApiController]
[Route("api/roles")]
public sealed class RolesController : ControllerBase
{
    private readonly IRoleService _roleService;

    public RolesController(IRoleService roleService) => _roleService = roleService;

    [HttpGet]
    public async Task<IActionResult> GetActive(CancellationToken cancellationToken) =>
        this.ToHttpResult(await _roleService.GetActiveAsync(cancellationToken));

    [HttpGet("permissions")]
    public async Task<IActionResult> GetOperationClaims(CancellationToken cancellationToken) =>
        this.ToHttpResult(await _roleService.GetOperationClaimsAsync(cancellationToken));

    [HttpPost]
    public Task<IActionResult> Create(RoleRequest request, CancellationToken cancellationToken) =>
        Mutate(actorId => _roleService.CreateAsync(request, actorId, cancellationToken));

    [HttpPut("{roleId:int}")]
    public Task<IActionResult> Update(int roleId, RoleRequest request, CancellationToken cancellationToken) =>
        Mutate(actorId => _roleService.UpdateAsync(roleId, request, actorId, cancellationToken));

    [HttpDelete("{roleId:int}")]
    public Task<IActionResult> Deactivate(int roleId, CancellationToken cancellationToken) =>
        Mutate(actorId => _roleService.DeactivateAsync(roleId, actorId, cancellationToken));

    [HttpPut("{roleId:int}/activate")]
    public Task<IActionResult> Activate(int roleId, CancellationToken cancellationToken) =>
        Mutate(actorId => _roleService.ActivateAsync(roleId, actorId, cancellationToken));

    [HttpGet("{roleId:int}/permissions")]
    public async Task<IActionResult> GetPermissions(int roleId, CancellationToken cancellationToken) =>
        this.ToHttpResult(await _roleService.GetPermissionsAsync(roleId, cancellationToken));

    [HttpGet("users/{userId:int}/permissions")]
    public async Task<IActionResult> GetUserPermissions(int userId, CancellationToken cancellationToken) =>
        this.ToHttpResult(await _roleService.GetUserClaimsAsync(userId, cancellationToken));

    [HttpPut("users/{userId:int}/permissions/{operationClaimId:int}")]
    public Task<IActionResult> GrantUserPermission(int userId, int operationClaimId, CancellationToken cancellationToken) =>
        Mutate(actorId => _roleService.GrantUserClaimAsync(userId, operationClaimId, actorId, cancellationToken));

    [HttpDelete("users/{userId:int}/permissions/{operationClaimId:int}")]
    public Task<IActionResult> RevokeUserPermission(int userId, int operationClaimId, CancellationToken cancellationToken) =>
        Mutate(actorId => _roleService.RevokeUserClaimAsync(userId, operationClaimId, actorId, cancellationToken));

    [HttpPut("{roleId:int}/users/{userId:int}")]
    public Task<IActionResult> AssignUser(int roleId, int userId, CancellationToken cancellationToken) =>
        Mutate((actorId) => _roleService.AssignToUserAsync(userId, roleId, actorId, cancellationToken));

    [HttpDelete("{roleId:int}/users/{userId:int}")]
    public Task<IActionResult> RemoveUser(int roleId, int userId, CancellationToken cancellationToken) =>
        Mutate((actorId) => _roleService.RemoveFromUserAsync(userId, roleId, actorId, cancellationToken));

    [HttpPut("{roleId:int}/permissions/{operationClaimId:int}")]
    public Task<IActionResult> GrantPermission(int roleId, int operationClaimId, CancellationToken cancellationToken) =>
        Mutate((actorId) => _roleService.GrantPermissionAsync(roleId, operationClaimId, actorId, cancellationToken));

    [HttpDelete("{roleId:int}/permissions/{operationClaimId:int}")]
    public Task<IActionResult> RevokePermission(int roleId, int operationClaimId, CancellationToken cancellationToken) =>
        Mutate((actorId) => _roleService.RevokePermissionAsync(roleId, operationClaimId, actorId, cancellationToken));

    private async Task<IActionResult> Mutate(System.Func<int, Task<Core.Utilities.Results.IResult>> operation)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var actorId)) return Unauthorized();
        return this.ToHttpResult(await operation(actorId));
    }
}
