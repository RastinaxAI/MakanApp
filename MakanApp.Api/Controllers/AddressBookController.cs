using MakanApp.Api.Authentication;
using MakanApp.Application.Messaging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MakanApp.Api.Controllers;

[ApiController, Authorize, Route("api/v1/address-book")]
public sealed class AddressBookController(AddressBookService service) : ControllerBase
{
    private AddressBookActor Actor() => new(AuthenticatedSession.GetUserId(User), AuthenticatedSession.GetSessionId(User));
    [HttpPut("devices/consent")]
    public async Task<ActionResult<ContactDeviceResult>> Consent(SetContactConsentCommand command, CancellationToken ct) => Ok(await service.ConsentAsync(Actor(), command, ct));
    [HttpPost("sync")]
    public async Task<ActionResult<ContactSyncResult>> Sync(ContactSyncCommand command, CancellationToken ct) => Ok(await service.SyncAsync(Actor(), command, ct));
    [HttpGet("delta")]
    public async Task<ActionResult<ContactDeltaResult>> Delta([FromQuery] Guid deviceId, [FromQuery] string? cursor, CancellationToken ct, [FromQuery] int limit = 100) =>
        Ok(await service.DeltaAsync(Actor(), deviceId, cursor, limit, ct));
}
