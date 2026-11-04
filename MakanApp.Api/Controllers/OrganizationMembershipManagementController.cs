using MakanApp.Api.Authentication;
using MakanApp.Application.Organization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MakanApp.Api.Controllers;

[ApiController, Authorize]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[Route("api/v1/organization/members/{membershipId:guid}")]
public sealed class OrganizationMembershipManagementController(SchoolMembershipManagementService service) : ControllerBase
{
    [HttpPost("roles")]
    [ProducesResponseType<SchoolMembershipMutationResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<SchoolMembershipMutationResult>> Grant(Guid membershipId, GrantSchoolRoleCommand command, CancellationToken ct) =>
        Ok(await service.GrantAsync(AuthenticatedSession.GetUserId(User), AuthenticatedSession.GetSessionId(User),
            membershipId, command, HttpContext.TraceIdentifier, ct));

    [HttpPut("roles/{roleAssignmentId:guid}")]
    [ProducesResponseType<SchoolMembershipMutationResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<SchoolMembershipMutationResult>> Change(Guid membershipId, Guid roleAssignmentId, ChangeSchoolRoleCommand command, CancellationToken ct) =>
        Ok(await service.ChangeAsync(AuthenticatedSession.GetUserId(User), AuthenticatedSession.GetSessionId(User),
            membershipId, roleAssignmentId, command, HttpContext.TraceIdentifier, ct));

    [HttpDelete("roles/{roleAssignmentId:guid}")]
    [ProducesResponseType<SchoolMembershipMutationResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<SchoolMembershipMutationResult>> EndRole(Guid membershipId, Guid roleAssignmentId, [FromBody] EndSchoolRoleCommand command, CancellationToken ct) =>
        Ok(await service.EndRoleAsync(AuthenticatedSession.GetUserId(User), AuthenticatedSession.GetSessionId(User),
            membershipId, roleAssignmentId, command, HttpContext.TraceIdentifier, ct));

    [HttpDelete]
    [ProducesResponseType<SchoolMembershipMutationResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<SchoolMembershipMutationResult>> EndMembership(Guid membershipId, [FromBody] EndSchoolMembershipCommand command, CancellationToken ct) =>
        Ok(await service.EndMembershipAsync(AuthenticatedSession.GetUserId(User), AuthenticatedSession.GetSessionId(User),
            membershipId, command, HttpContext.TraceIdentifier, ct));
}
