using MakanApp.Api.Authentication;
using MakanApp.Application.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace MakanApp.Api.Controllers;

[ApiController, Authorize, Route("api/v1/me/phone-change/requests")]
public sealed class PhoneChangeController(PhoneChangeService service) : ControllerBase
{
    [HttpPost, EnableRateLimiting("otp")]
    public async Task<ActionResult<PhoneChangeResult>> RequestChange(RequestPhoneChangeCommand command, CancellationToken ct) =>
        Ok(await service.RequestAsync(Actor(), command, ct));
    [HttpPost("{requestId:guid}/verify"), EnableRateLimiting("otp-verify")]
    public async Task<ActionResult<PhoneChangeResult>> Verify(Guid requestId, VerifyPhoneChangeCommand command, CancellationToken ct) =>
        Ok(await service.VerifyAsync(Actor(), requestId, command, HttpContext.TraceIdentifier, ct));
    private PhoneChangeActor Actor() => new(AuthenticatedSession.GetUserId(User), AuthenticatedSession.GetSessionId(User));
}
