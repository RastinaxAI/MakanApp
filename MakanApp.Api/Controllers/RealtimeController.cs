using MakanApp.Api.Authentication;
using MakanApp.Application.Realtime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace MakanApp.Api.Controllers;

[ApiController]
[Authorize]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[Route("api/v1/realtime")]
public sealed class RealtimeController(IRealtimeSyncService sync) : ControllerBase
{
    /// <summary>بازیابی مجاز حساب؛ بدون cursor ابتدا مرز Snapshot دریافت می‌شود.</summary>
    [HttpGet("changes")]
    [EnableRateLimiting("realtime-replay")]
    [ProducesResponseType<AccountChangePageResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AccountChangePageResult>> Changes([FromQuery] string? cursor = null,
        [FromQuery] int limit = 100, CancellationToken cancellationToken = default) =>
        Ok(await sync.ReadAsync(AuthenticatedSession.GetUserId(User), AuthenticatedSession.GetSessionId(User), cursor, limit, cancellationToken));
}
