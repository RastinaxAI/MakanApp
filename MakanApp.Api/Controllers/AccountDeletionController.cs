using MakanApp.Api.Authentication;
using MakanApp.Application.Identity;
using MakanApp.Domain.Platform;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MakanApp.Api.Controllers;

[ApiController, Authorize, Route("api/v1/me/deletion-request")]
public sealed class AccountDeletionController(AccountDeletionService service) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<AccountDeletionStatusResult>(StatusCodes.Status202Accepted)]
    public async Task<ActionResult<AccountDeletionStatusResult>> CreateRequest(RequestAccountDeletionCommand command, CancellationToken ct) =>
        StatusCode(202, await service.RequestAsync(AuthenticatedSession.GetUserId(User), AuthenticatedSession.GetSessionId(User), command, HttpContext.TraceIdentifier, ct));
    [HttpGet]
    [ProducesResponseType<AccountDeletionStatusResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult<AccountDeletionStatusResult?>> Status(CancellationToken ct) =>
        Ok(await service.StatusAsync(AuthenticatedSession.GetUserId(User), AuthenticatedSession.GetSessionId(User), ct));
}

[ApiController, Authorize(Policy = PlatformPermissionKeys.PrivacyRequestsRead), Route("api/admin/v1/privacy/deletion-requests")]
public sealed class PlatformDeletionRequestsController(AccountDeletionService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<AccountDeletionPage>> Queue(CancellationToken ct, [FromQuery] int page = 1, [FromQuery] int pageSize = 50) =>
        Ok(await service.PageAsync(AuthenticatedSession.GetUserId(User), AuthenticatedSession.GetSessionId(User), page, pageSize, ct));

    [HttpGet("{requestId:guid}")]
    [ProducesResponseType<AccountDeletionDetail>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AccountDeletionDetail>> Detail(Guid requestId, CancellationToken ct) =>
        Ok(await service.DetailAsync(AuthenticatedSession.GetUserId(User), AuthenticatedSession.GetSessionId(User), requestId, ct));

    [HttpPost("{requestId:guid}/retry"), Authorize(Policy = PlatformPermissionKeys.PrivacyRequestsManage)]
    [ProducesResponseType<AccountDeletionStatusResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AccountDeletionStatusResult>> Retry(Guid requestId, RetryAccountDeletionCommand command, CancellationToken ct) =>
        Ok(await service.RetryAsync(AuthenticatedSession.GetUserId(User), AuthenticatedSession.GetSessionId(User), requestId, command, ct));
}
