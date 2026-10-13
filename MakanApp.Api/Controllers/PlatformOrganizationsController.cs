using MakanApp.Api.Authentication;
using MakanApp.Application.Platform;
using MakanApp.Domain.Organization;
using MakanApp.Domain.Platform;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MakanApp.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/admin/v1/organizations")]
public sealed class PlatformOrganizationsController(
    IPlatformOperationsService operationsService) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = PlatformPermissionKeys.OrganizationsRead)]
    public async Task<ActionResult<PlatformOrganizationPage>> GetOrganizations(
        [FromQuery] string? search,
        [FromQuery] OrganizationStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 30,
        [FromQuery] string sortBy = "createdAtUtc",
        [FromQuery] string sortDirection = "desc",
        CancellationToken cancellationToken = default) =>
        Ok(await operationsService.GetOrganizationsAsync(
            new PlatformOrganizationQuery(search, status, page, pageSize, sortBy, sortDirection),
            cancellationToken));

    [HttpGet("{organizationId:guid}")]
    [Authorize(Policy = PlatformPermissionKeys.OrganizationsRead)]
    public async Task<ActionResult<PlatformOrganizationDetail>> GetOrganization(
        Guid organizationId,
        CancellationToken cancellationToken) =>
        Ok(await operationsService.GetOrganizationAsync(organizationId, cancellationToken));

    [HttpGet("{organizationId:guid}/suspension-impact")]
    [Authorize(Policy = PlatformPermissionKeys.OrganizationsRead)]
    public async Task<ActionResult<PlatformOrganizationImpactPreview>> GetSuspensionImpact(
        Guid organizationId,
        CancellationToken cancellationToken) =>
        Ok(await operationsService.GetOrganizationImpactAsync(organizationId, cancellationToken));

    [HttpPost]
    [Authorize(Policy = PlatformPermissionKeys.OrganizationsManage)]
    public async Task<ActionResult<PlatformProvisioningResult>> CreateOrganization(
        CreatePlatformOrganizationCommand command,
        CancellationToken cancellationToken)
    {
        var result = await operationsService.CreateOrganizationAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            command,
            HttpContext.TraceIdentifier,
            cancellationToken);
        return result.Replayed ? Ok(result) : StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpPost("{organizationId:guid}/suspend")]
    [Authorize(Policy = PlatformPermissionKeys.OrganizationsManage)]
    public async Task<ActionResult<PlatformOrganizationMutationResult>> Suspend(
        Guid organizationId,
        PlatformOrganizationStateCommand command,
        CancellationToken cancellationToken) =>
        Ok(await operationsService.SuspendOrganizationAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            organizationId,
            command,
            HttpContext.TraceIdentifier,
            cancellationToken));

    [HttpPost("{organizationId:guid}/reactivate")]
    [Authorize(Policy = PlatformPermissionKeys.OrganizationsManage)]
    public async Task<ActionResult<PlatformOrganizationMutationResult>> Reactivate(
        Guid organizationId,
        PlatformOrganizationStateCommand command,
        CancellationToken cancellationToken) =>
        Ok(await operationsService.ReactivateOrganizationAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            organizationId,
            command,
            HttpContext.TraceIdentifier,
            cancellationToken));

    [HttpGet("{organizationId:guid}/memberships")]
    [Authorize(Policy = PlatformPermissionKeys.OrganizationsRead)]
    public async Task<ActionResult<PlatformMembershipPage>> GetMemberships(
        Guid organizationId,
        [FromQuery] bool activeOnly = true,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default) =>
        Ok(await operationsService.GetMembershipsAsync(
            organizationId,
            new PlatformMembershipQuery(activeOnly, page, pageSize),
            cancellationToken));

    [HttpPost("{organizationId:guid}/invitations")]
    [Authorize(Policy = PlatformPermissionKeys.OrganizationsManage)]
    public async Task<ActionResult<PlatformInvitationResult>> CreateInvitation(
        Guid organizationId,
        CreatePlatformInvitationCommand command,
        CancellationToken cancellationToken)
    {
        var result = await operationsService.CreateInvitationAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            organizationId,
            command,
            HttpContext.TraceIdentifier,
            cancellationToken);
        return result.Replayed ? Ok(result) : StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpPut("{organizationId:guid}/memberships/{membershipId:guid}/role")]
    [Authorize(Policy = PlatformPermissionKeys.OrganizationsManage)]
    public async Task<ActionResult<PlatformMembershipMutationResult>> ChangeRole(
        Guid organizationId,
        Guid membershipId,
        ChangePlatformMembershipRoleCommand command,
        CancellationToken cancellationToken) =>
        Ok(await operationsService.ChangeMembershipRoleAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            organizationId,
            membershipId,
            command,
            HttpContext.TraceIdentifier,
            cancellationToken));

    [HttpDelete("{organizationId:guid}/memberships/{membershipId:guid}")]
    [Authorize(Policy = PlatformPermissionKeys.OrganizationsManage)]
    public async Task<ActionResult<PlatformMembershipMutationResult>> EndMembership(
        Guid organizationId,
        Guid membershipId,
        [FromBody] EndPlatformMembershipCommand command,
        CancellationToken cancellationToken) =>
        Ok(await operationsService.EndMembershipAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            organizationId,
            membershipId,
            command,
            HttpContext.TraceIdentifier,
            cancellationToken));
}
