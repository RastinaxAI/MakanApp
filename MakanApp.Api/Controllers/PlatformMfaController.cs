using MakanApp.Api.Authentication;
using MakanApp.Application.Platform;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace MakanApp.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/admin/v1/mfa")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[EnableRateLimiting("platform-mfa")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
public sealed class PlatformMfaController(PlatformMfaService service) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PlatformMfaResult>(StatusCodes.Status200OK)]
    public Task<PlatformMfaResult> Status(CancellationToken ct) => service.GetStatusAsync(UserId, SessionId, ct);

    [HttpPost("enrollment")]
    [ProducesResponseType<PlatformMfaResult>(StatusCodes.Status200OK)]
    public Task<PlatformMfaResult> StartEnrollment(CancellationToken ct) =>
        service.StartEnrollmentAsync(UserId, SessionId, HttpContext.TraceIdentifier, ct);

    [HttpPost("enrollment/confirm")]
    [ProducesResponseType<PlatformMfaResult>(StatusCodes.Status200OK)]
    public Task<PlatformMfaResult> Confirm(ConfirmPlatformMfaCommand command, CancellationToken ct) =>
        service.ConfirmAsync(UserId, SessionId, command, HttpContext.TraceIdentifier, ct);

    [HttpPost("verify")]
    [ProducesResponseType<PlatformMfaResult>(StatusCodes.Status200OK)]
    public Task<PlatformMfaResult> Verify(VerifyPlatformMfaCommand command, CancellationToken ct) =>
        service.VerifyAsync(UserId, SessionId, command, false, HttpContext.TraceIdentifier, ct);

    [HttpPost("recovery/verify")]
    [ProducesResponseType<PlatformMfaResult>(StatusCodes.Status200OK)]
    public Task<PlatformMfaResult> VerifyRecovery(VerifyPlatformMfaCommand command, CancellationToken ct) =>
        service.VerifyAsync(UserId, SessionId, command, true, HttpContext.TraceIdentifier, ct);

    [HttpPost("recovery-codes/regenerate")]
    [ProducesResponseType<PlatformMfaResult>(StatusCodes.Status200OK)]
    public Task<PlatformMfaResult> Regenerate(CancellationToken ct) =>
        service.RegenerateRecoveryCodesAsync(UserId, SessionId, HttpContext.TraceIdentifier, ct);

    [HttpPost("users/{userId:guid}/reset")]
    [ProducesResponseType<PlatformMfaResult>(StatusCodes.Status200OK)]
    public Task<PlatformMfaResult> Reset(Guid userId, ResetPlatformMfaCommand command, CancellationToken ct) =>
        service.ResetAsync(UserId, SessionId, userId, command, HttpContext.TraceIdentifier, ct);

    private Guid UserId => AuthenticatedSession.GetUserId(User);
    private Guid SessionId => AuthenticatedSession.GetSessionId(User);
}
