using MakanApp.Api.Authentication;
using MakanApp.Application.Support;
using MakanApp.Domain.Support;
using MakanApp.Domain.Platform;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MakanApp.Api.Controllers;

[ApiController, Authorize, Route("api/v1/support/tickets")]
public sealed class SupportTicketsController(SupportTicketService service) : ControllerBase
{
    private SupportActor Actor() => new(AuthenticatedSession.GetUserId(User), AuthenticatedSession.GetSessionId(User), HttpContext.TraceIdentifier);
    [HttpPost]
    [ProducesResponseType<SupportTicketResult>(StatusCodes.Status201Created)]
    public async Task<ActionResult<SupportTicketResult>> Create(CreateSupportTicketCommand command, CancellationToken ct) => StatusCode(201, await service.CreateAsync(Actor(), command, ct));
    [HttpGet]
    public async Task<ActionResult<SupportTicketPage>> List(CancellationToken ct, [FromQuery] SupportTicketStatus? status = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 50) =>
        Ok(await service.PageAsync(Actor(), false, status, page, pageSize, ct));
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SupportTicketResult>> Read(Guid id, CancellationToken ct, [FromQuery] long afterMessageVersion = 0) => Ok(await service.ReadAsync(Actor(), id, false, ct, afterMessageVersion));
    [HttpPost("{id:guid}/messages")]
    public async Task<ActionResult<SupportTicketResult>> Reply(Guid id, ReplySupportTicketCommand command, CancellationToken ct) => Ok(await service.ReplyAsync(Actor(), id, command, false, ct));
}

[ApiController, Authorize(Policy = PlatformPermissionKeys.SupportRead), Route("api/admin/v1/support/tickets")]
public sealed class PlatformSupportTicketsController(SupportTicketService service) : ControllerBase
{
    private SupportActor Actor() => new(AuthenticatedSession.GetUserId(User), AuthenticatedSession.GetSessionId(User), HttpContext.TraceIdentifier);
    [HttpGet]
    public async Task<ActionResult<SupportTicketPage>> Queue(CancellationToken ct, [FromQuery] SupportTicketStatus? status = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 50) =>
        Ok(await service.PageAsync(Actor(), true, status, page, pageSize, ct));
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SupportTicketResult>> Read(Guid id, CancellationToken ct, [FromQuery] long afterMessageVersion = 0) => Ok(await service.ReadAsync(Actor(), id, true, ct, afterMessageVersion));
    [HttpPost("{id:guid}/messages"), Authorize(Policy = PlatformPermissionKeys.SupportManage)]
    public async Task<ActionResult<SupportTicketResult>> Reply(Guid id, ReplySupportTicketCommand command, CancellationToken ct) => Ok(await service.ReplyAsync(Actor(), id, command, true, ct));
    [HttpPost("{id:guid}/assign"), Authorize(Policy = PlatformPermissionKeys.SupportManage)]
    public async Task<ActionResult<SupportTicketResult>> Assign(Guid id, ManageSupportTicketCommand command, CancellationToken ct) => Ok(await service.ManageAsync(Actor(), id, command, false, ct));
    [HttpPost("{id:guid}/resolve"), Authorize(Policy = PlatformPermissionKeys.SupportManage)]
    public async Task<ActionResult<SupportTicketResult>> Resolve(Guid id, ManageSupportTicketCommand command, CancellationToken ct) => Ok(await service.ManageAsync(Actor(), id, command, true, ct));
}
