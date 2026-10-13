using MakanApp.Api.Authentication;
using MakanApp.Application.Platform;
using MakanApp.Domain.Identity;
using MakanApp.Domain.Platform;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MakanApp.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/admin/v1/users")]
public sealed class PlatformUsersController(
    IPlatformOperationsService operationsService) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = PlatformPermissionKeys.UsersRead)]
    public async Task<ActionResult<PlatformUserPage>> GetUsers(
        [FromQuery] string? search,
        [FromQuery] UserAccountStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 30,
        [FromQuery] string sortBy = "createdAtUtc",
        [FromQuery] string sortDirection = "desc",
        CancellationToken cancellationToken = default) =>
        Ok(await operationsService.GetUsersAsync(
            new PlatformUserQuery(search, status, page, pageSize, sortBy, sortDirection),
            cancellationToken));

    [HttpGet("{userId:guid}")]
    [Authorize(Policy = PlatformPermissionKeys.UsersRead)]
    public async Task<ActionResult<PlatformUserDetail>> GetUser(
        Guid userId,
        CancellationToken cancellationToken) =>
        Ok(await operationsService.GetUserAsync(userId, cancellationToken));

    [HttpPost("{userId:guid}/suspend")]
    [Authorize(Policy = PlatformPermissionKeys.UsersManage)]
    public async Task<ActionResult<PlatformUserMutationResult>> Suspend(
        Guid userId,
        PlatformUserStateCommand command,
        CancellationToken cancellationToken) =>
        Ok(await operationsService.SuspendUserAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            userId,
            command,
            HttpContext.TraceIdentifier,
            cancellationToken));

    [HttpPost("{userId:guid}/reactivate")]
    [Authorize(Policy = PlatformPermissionKeys.UsersManage)]
    public async Task<ActionResult<PlatformUserMutationResult>> Reactivate(
        Guid userId,
        PlatformUserStateCommand command,
        CancellationToken cancellationToken) =>
        Ok(await operationsService.ReactivateUserAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            userId,
            command,
            HttpContext.TraceIdentifier,
            cancellationToken));

    [HttpPost("{userId:guid}/sessions/revoke")]
    [Authorize(Policy = PlatformPermissionKeys.SessionsManage)]
    public async Task<ActionResult<PlatformSessionRevocationResult>> RevokeAllSessions(
        Guid userId,
        RevokePlatformSessionsCommand command,
        CancellationToken cancellationToken) =>
        Ok(await operationsService.RevokeSessionsAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            userId,
            null,
            command,
            HttpContext.TraceIdentifier,
            cancellationToken));

    [HttpPost("{userId:guid}/sessions/{sessionId:guid}/revoke")]
    [Authorize(Policy = PlatformPermissionKeys.SessionsManage)]
    public async Task<ActionResult<PlatformSessionRevocationResult>> RevokeSession(
        Guid userId,
        Guid sessionId,
        RevokePlatformSessionsCommand command,
        CancellationToken cancellationToken) =>
        Ok(await operationsService.RevokeSessionsAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            userId,
            sessionId,
            command,
            HttpContext.TraceIdentifier,
            cancellationToken));
}
