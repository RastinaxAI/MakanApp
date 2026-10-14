using MakanApp.Api.Authentication;
using MakanApp.Application.Organization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MakanApp.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/organization/people")]
public sealed class OrganizationPeopleController(IOrganizationPersonService service) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<OrganizationPersonDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<OrganizationPersonDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<OrganizationPersonDto>> Create(CreateOrganizationPersonCommand command, CancellationToken cancellationToken)
    {
        var result = await service.CreateAsync(AuthenticatedSession.GetUserId(User), AuthenticatedSession.GetSessionId(User), command, cancellationToken);
        return StatusCode(result.Replayed ? StatusCodes.Status200OK : StatusCodes.Status201Created, result);
    }
    [HttpGet]
    public async Task<ActionResult<OrganizationPeoplePage>> List([FromQuery] string? search,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken cancellationToken = default) =>
        Ok(await service.ListAsync(AuthenticatedSession.GetUserId(User), AuthenticatedSession.GetSessionId(User), search, page, pageSize, cancellationToken));
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<OrganizationPersonDto>> Get(Guid id, CancellationToken cancellationToken) =>
        Ok(await service.GetAsync(AuthenticatedSession.GetUserId(User), AuthenticatedSession.GetSessionId(User), id, cancellationToken));
    [HttpPatch("{id:guid}")]
    public async Task<ActionResult<OrganizationPersonDto>> Update(Guid id, UpdateOrganizationPersonNamesCommand command, CancellationToken cancellationToken) =>
        Ok(await service.UpdateAsync(AuthenticatedSession.GetUserId(User), AuthenticatedSession.GetSessionId(User), id, command, cancellationToken));
    [HttpPost("{id:guid}/end")]
    public async Task<ActionResult<OrganizationPersonDto>> End(Guid id, OnboardingStateCommand command, CancellationToken cancellationToken) =>
        Ok(await service.EndAsync(AuthenticatedSession.GetUserId(User), AuthenticatedSession.GetSessionId(User), id, command, cancellationToken));
    [HttpPost("{id:guid}/invitations")]
    [ProducesResponseType<PersonLinkInvitationDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<PersonLinkInvitationDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PersonLinkInvitationDto>> Invite(Guid id, CreatePersonLinkInvitationCommand command, CancellationToken cancellationToken)
    {
        var result = await service.InviteAsync(AuthenticatedSession.GetUserId(User), AuthenticatedSession.GetSessionId(User), id, command, cancellationToken);
        return StatusCode(result.Replayed ? StatusCodes.Status200OK : StatusCodes.Status201Created, result);
    }
    [HttpPost("{id:guid}/invitations/{invitationId:guid}/revoke")]
    public async Task<ActionResult<PersonLinkInvitationDto>> RevokeInvitation(Guid id, Guid invitationId, OnboardingStateCommand command, CancellationToken cancellationToken) =>
        Ok(await service.RevokeInvitationAsync(AuthenticatedSession.GetUserId(User), AuthenticatedSession.GetSessionId(User), id, invitationId, command, cancellationToken));
}
