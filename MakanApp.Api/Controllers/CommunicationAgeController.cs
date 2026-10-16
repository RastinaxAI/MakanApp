using MakanApp.Api.Authentication;
using MakanApp.Application.Messaging;
using MakanApp.Domain.Platform;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MakanApp.Api.Controllers;

[ApiController, Authorize]
public sealed class CommunicationAgeController(CommunicationAgeService service) : ControllerBase
{
    [HttpGet("api/admin/v1/users/{userId:guid}/communication-age")]
    [Authorize(Policy = PlatformPermissionKeys.UsersManage)]
    public async Task<ActionResult<CommunicationAgeResult>> GetPlatform(Guid userId, CancellationToken ct) =>
        Ok(await service.GetPlatformAsync(AuthenticatedSession.GetUserId(User), AuthenticatedSession.GetSessionId(User),
            userId, HttpContext.TraceIdentifier, ct));

    [HttpPost("api/admin/v1/users/{userId:guid}/communication-age/verifications")]
    [Authorize(Policy = PlatformPermissionKeys.UsersManage)]
    public async Task<ActionResult<CommunicationAgeResult>> VerifyInitial(Guid userId, VerifyCommunicationAgeCommand command, CancellationToken ct) =>
        Ok(await service.VerifyInitialAsync(AuthenticatedSession.GetUserId(User), AuthenticatedSession.GetSessionId(User),
            userId, command, HttpContext.TraceIdentifier, ct));

    [HttpGet("api/v1/me/messaging-status")]
    public async Task<ActionResult<MessagingStatusResult>> GetSelfStatus(CancellationToken ct) =>
        Ok(await service.GetSelfStatusAsync(AuthenticatedSession.GetUserId(User), AuthenticatedSession.GetSessionId(User), ct));

    [HttpGet("api/v1/organization/people/{personId:guid}/communication-age")]
    public async Task<ActionResult<CommunicationAgeResult>> Get(Guid personId, CancellationToken ct) =>
        Ok(await service.GetSchoolAsync(AuthenticatedSession.GetUserId(User), AuthenticatedSession.GetSessionId(User), personId, ct));

    [HttpPost("api/v1/organization/people/{personId:guid}/communication-age/verify")]
    public async Task<ActionResult<CommunicationAgeResult>> Verify(Guid personId, VerifyCommunicationAgeCommand command, CancellationToken ct) =>
        Ok(await service.VerifySchoolAsync(AuthenticatedSession.GetUserId(User), AuthenticatedSession.GetSessionId(User), personId, command, HttpContext.TraceIdentifier, ct));

    [HttpPost("api/admin/v1/users/{userId:guid}/communication-age/resolve")]
    [Authorize(Policy = PlatformPermissionKeys.UsersManage)]
    public async Task<ActionResult<CommunicationAgeResult>> Resolve(Guid userId, VerifyCommunicationAgeCommand command, CancellationToken ct) =>
        Ok(await service.ResolveConflictAsync(AuthenticatedSession.GetUserId(User), AuthenticatedSession.GetSessionId(User), userId, command, HttpContext.TraceIdentifier, ct));
}
