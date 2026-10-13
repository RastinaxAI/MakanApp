using MakanApp.Api.Authentication;
using MakanApp.Application.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace MakanApp.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController(IIdentityService identityService) : ControllerBase
{
    [HttpPost("otp/challenges")]
    [EnableRateLimiting("otp")]
    [ProducesResponseType<RequestOtpResult>(StatusCodes.Status202Accepted)]
    public async Task<ActionResult<RequestOtpResult>> RequestOtp(
        RequestOtpCommand command,
        CancellationToken cancellationToken)
    {
        var result = await identityService.RequestOtpAsync(command, cancellationToken);
        return Accepted(result);
    }

    [HttpPost("otp/verify")]
    [EnableRateLimiting("otp-verify")]
    [ProducesResponseType<VerifyOtpResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<VerifyOtpResult>> VerifyOtp(
        VerifyOtpCommand command,
        CancellationToken cancellationToken)
    {
        var result = await identityService.VerifyOtpAsync(command, cancellationToken);
        return Ok(result);
    }

    [HttpPost("refresh")]
    [EnableRateLimiting("session-refresh")]
    [ProducesResponseType<RefreshSessionResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<RefreshSessionResult>> Refresh(
        RefreshSessionCommand command,
        CancellationToken cancellationToken) =>
        Ok(await identityService.RefreshSessionAsync(command, cancellationToken));

    [Authorize]
    [HttpGet("sessions")]
    [ProducesResponseType<IReadOnlyCollection<UserSessionResult>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<UserSessionResult>>> GetSessions(
        CancellationToken cancellationToken) =>
        Ok(await identityService.GetSessionsAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            cancellationToken));

    [Authorize]
    [HttpDelete("sessions/{sessionId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RevokeSession(
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        await identityService.RevokeSessionAsync(
            AuthenticatedSession.GetUserId(User),
            sessionId,
            cancellationToken);
        return NoContent();
    }

    [Authorize]
    [HttpPost("sessions/revoke-others")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RevokeOtherSessions(CancellationToken cancellationToken)
    {
        await identityService.RevokeOtherSessionsAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            cancellationToken);
        return NoContent();
    }

    [Authorize]
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        await identityService.LogoutAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            cancellationToken);
        return NoContent();
    }
}
