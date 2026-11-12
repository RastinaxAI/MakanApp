using MakanApp.Api.Authentication;
using MakanApp.Application.Organization;
using MakanApp.Domain.Organization;
using MakanApp.Domain.Platform;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MakanApp.Api.Controllers;

[ApiController, Authorize, Route("api/v1/school-applications")]
public sealed class SchoolApplicationsController(SchoolApplicationService service) : ControllerBase
{
    private SchoolApplicationActor Actor() => new(AuthenticatedSession.GetUserId(User), AuthenticatedSession.GetSessionId(User), HttpContext.TraceIdentifier);
    [HttpPost]
    [ProducesResponseType<SchoolApplicationResult>(StatusCodes.Status201Created)]
    public async Task<ActionResult<SchoolApplicationResult>> Create(CreateSchoolApplicationCommand command, CancellationToken ct) => StatusCode(201, await service.CreateAsync(Actor(), command, ct));
    [HttpGet]
    public async Task<ActionResult<SchoolApplicationPage>> List(CancellationToken ct, [FromQuery] SchoolApplicationStatus? status = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 50) =>
        Ok(await service.PageAsync(Actor(), false, status, page, pageSize, ct));
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SchoolApplicationResult>> Read(Guid id, CancellationToken ct, [FromQuery] int? revision = null) => Ok(await service.ReadAsync(Actor(), id, false, revision, ct));
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<SchoolApplicationResult>> Update(Guid id, UpdateSchoolApplicationCommand command, CancellationToken ct) => Ok(await service.UpdateAsync(Actor(), id, command, ct));
    [HttpPost("{id:guid}/submit")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SchoolApplicationResult>> Submit(Guid id, SchoolApplicationActionCommand command, CancellationToken ct) => Ok(await service.SubmitAsync(Actor(), id, command, ct));
}

[ApiController, Authorize(Policy = PlatformPermissionKeys.SchoolApplicationsRead), Route("api/admin/v1/school-applications")]
public sealed class PlatformSchoolApplicationsController(SchoolApplicationService service) : ControllerBase
{
    private SchoolApplicationActor Actor() => new(AuthenticatedSession.GetUserId(User), AuthenticatedSession.GetSessionId(User), HttpContext.TraceIdentifier);
    [HttpGet]
    public async Task<ActionResult<SchoolApplicationPage>> Queue(CancellationToken ct, [FromQuery] SchoolApplicationStatus? status = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 50) =>
        Ok(await service.PageAsync(Actor(), true, status, page, pageSize, ct));
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SchoolApplicationResult>> Read(Guid id, CancellationToken ct, [FromQuery] int? revision = null) => Ok(await service.ReadAsync(Actor(), id, true, revision, ct));
    [HttpPost("{id:guid}/review"), Authorize(Policy = PlatformPermissionKeys.SchoolApplicationsReview)]
    public async Task<ActionResult<SchoolApplicationResult>> StartReview(Guid id, SchoolApplicationActionCommand command, CancellationToken ct) => Ok(await service.ReviewAsync(Actor(), id, command, true, ct));
    [HttpPost("{id:guid}/decisions"), Authorize(Policy = PlatformPermissionKeys.SchoolApplicationsReview)]
    public async Task<ActionResult<SchoolApplicationResult>> Decide(Guid id, SchoolApplicationActionCommand command, CancellationToken ct) => Ok(await service.ReviewAsync(Actor(), id, command, false, ct));
}
