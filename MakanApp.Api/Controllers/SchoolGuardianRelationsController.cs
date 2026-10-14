using MakanApp.Api.Authentication;
using MakanApp.Application.Guardian;
using MakanApp.Application.Organization;
using MakanApp.Domain.Guardian;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MakanApp.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/organization/guardian-relations")]
public sealed class SchoolGuardianRelationsController(IGuardianManagementService service) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<SchoolGuardianRelationDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<SchoolGuardianRelationDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<SchoolGuardianRelationDto>> Create(CreateSchoolGuardianRelationCommand command, CancellationToken cancellationToken)
    {
        var result = await service.CreateAsync(AuthenticatedSession.GetUserId(User), AuthenticatedSession.GetSessionId(User), command, cancellationToken);
        return StatusCode(result.Replayed ? StatusCodes.Status200OK : StatusCodes.Status201Created, result);
    }
    [HttpGet]
    public async Task<ActionResult<SchoolGuardianRelationsPage>> List([FromQuery] Guid? learnerId,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken cancellationToken = default) =>
        Ok(await service.ListAsync(AuthenticatedSession.GetUserId(User), AuthenticatedSession.GetSessionId(User), learnerId, page, pageSize, cancellationToken));
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SchoolGuardianRelationDto>> Get(Guid id, CancellationToken cancellationToken) =>
        Ok(await service.GetAsync(AuthenticatedSession.GetUserId(User), AuthenticatedSession.GetSessionId(User), id, cancellationToken));
    [HttpPost("{id:guid}/activate")]
    public async Task<ActionResult<SchoolGuardianRelationDto>> Activate(Guid id, OnboardingStateCommand command, CancellationToken cancellationToken) =>
        Ok(await service.TransitionAsync(AuthenticatedSession.GetUserId(User), AuthenticatedSession.GetSessionId(User), id, GuardianRelationStatus.Active, command, cancellationToken));
    [HttpPost("{id:guid}/end")]
    public async Task<ActionResult<SchoolGuardianRelationDto>> End(Guid id, OnboardingStateCommand command, CancellationToken cancellationToken) =>
        Ok(await service.TransitionAsync(AuthenticatedSession.GetUserId(User), AuthenticatedSession.GetSessionId(User), id, GuardianRelationStatus.Ended, command, cancellationToken));
    [HttpPost("{id:guid}/revoke")]
    public async Task<ActionResult<SchoolGuardianRelationDto>> Revoke(Guid id, OnboardingStateCommand command, CancellationToken cancellationToken) =>
        Ok(await service.TransitionAsync(AuthenticatedSession.GetUserId(User), AuthenticatedSession.GetSessionId(User), id, GuardianRelationStatus.Revoked, command, cancellationToken));
}
