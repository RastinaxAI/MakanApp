using MakanApp.Api.Authentication;
using MakanApp.Application.Platform;
using MakanApp.Domain.Platform;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MakanApp.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/admin/v1")]
public sealed class PlatformIamController(IPlatformIamService platformIamService) : ControllerBase
{
    [HttpGet("me")]
    [Authorize(Policy = PlatformPermissionKeys.Access)]
    [ProducesResponseType<PlatformMeResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PlatformMeResult>> GetMe(CancellationToken cancellationToken) =>
        Ok(await platformIamService.GetMeAsync(
            AuthenticatedSession.GetUserId(User),
            cancellationToken));

    [HttpGet("roles")]
    [Authorize(Policy = PlatformPermissionKeys.RolesRead)]
    [ProducesResponseType<IReadOnlyCollection<PlatformRoleCatalogItem>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<PlatformRoleCatalogItem>>> GetRoles(
        CancellationToken cancellationToken) =>
        Ok(await platformIamService.GetRoleCatalogAsync(cancellationToken));

    [HttpGet("role-assignments")]
    [Authorize(Policy = PlatformPermissionKeys.RolesRead)]
    [ProducesResponseType<PlatformRoleAssignmentPage>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PlatformRoleAssignmentPage>> GetAssignments(
        [FromQuery] Guid? userId,
        [FromQuery] string? roleKey,
        [FromQuery] bool activeOnly = true,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 30,
        CancellationToken cancellationToken = default) =>
        Ok(await platformIamService.GetAssignmentsAsync(
            new PlatformRoleAssignmentQuery(userId, roleKey, activeOnly, page, pageSize),
            cancellationToken));

    [HttpPost("role-assignments")]
    [Authorize(Policy = PlatformPermissionKeys.RolesManage)]
    [ProducesResponseType<PlatformRoleMutationResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<PlatformRoleMutationResult>(StatusCodes.Status201Created)]
    public async Task<ActionResult<PlatformRoleMutationResult>> GrantRole(
        GrantPlatformRoleCommand command,
        CancellationToken cancellationToken)
    {
        var result = await platformIamService.GrantRoleAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            command,
            HttpContext.TraceIdentifier,
            cancellationToken);
        return result.Replayed
            ? Ok(result)
            : StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpDelete("role-assignments/{assignmentId:guid}")]
    [Authorize(Policy = PlatformPermissionKeys.RolesManage)]
    [ProducesResponseType<PlatformRoleMutationResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PlatformRoleMutationResult>> EndRole(
        Guid assignmentId,
        [FromBody] EndPlatformRoleAssignmentCommand command,
        CancellationToken cancellationToken) =>
        Ok(await platformIamService.EndRoleAssignmentAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            assignmentId,
            command,
            HttpContext.TraceIdentifier,
            cancellationToken));
}
