using MakanApp.Api.Authentication;
using MakanApp.Application.Commerce;
using MakanApp.Domain.Commerce;
using MakanApp.Domain.Platform;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MakanApp.Api.Controllers;

[ApiController, Authorize, Route("api/admin/v1/commerce")]
public sealed class PlatformCommercialContractsController(CommercialAdminService service) : ControllerBase
{
    private Guid Actor => AuthenticatedSession.GetUserId(User);
    private Guid Session => AuthenticatedSession.GetSessionId(User);
    [HttpGet("users/{ownerId:guid}/subscription"), Authorize(Policy = PlatformPermissionKeys.CommerceRead)]
    public async Task<ActionResult<CommercialSummaryDto>> IndividualSummary(Guid ownerId, CancellationToken ct) =>
        Ok(await service.SummaryAsync(Actor, Session, SubscriptionOwnerType.Individual, ownerId, ct));
    [HttpGet("organizations/{ownerId:guid}/subscription"), Authorize(Policy = PlatformPermissionKeys.CommerceRead)]
    public async Task<ActionResult<CommercialSummaryDto>> OrganizationSummary(Guid ownerId, CancellationToken ct) =>
        Ok(await service.SummaryAsync(Actor, Session, SubscriptionOwnerType.Organization, ownerId, ct));
    [HttpPut("plans/{planId:guid}/availability"), Authorize(Policy = PlatformPermissionKeys.CommerceManage)]
    public async Task<IActionResult> Availability(Guid planId, CommercialAvailabilityCommand command, CancellationToken ct)
    { await service.SetAvailabilityAsync(Actor, Session, planId, command, ct); return NoContent(); }
    [HttpPut("plan-versions/{versionId:guid}/draft"), Authorize(Policy = PlatformPermissionKeys.CommerceManage)]
    public async Task<ActionResult<CommercialValidationDto>> Draft(Guid versionId, CommercialDraftCommand command, CancellationToken ct) =>
        Ok(await service.UpdateDraftAsync(Actor, Session, versionId, command, ct));
    [HttpGet("plan-versions/{versionId:guid}/validation"), Authorize(Policy = PlatformPermissionKeys.CommerceRead)]
    public async Task<ActionResult<CommercialValidationDto>> Validate(Guid versionId, CancellationToken ct) =>
        Ok(await service.ValidateAsync(Actor, Session, versionId, ct));
    [HttpPost("organizations/{ownerId:guid}/commercial-contracts"), Authorize(Policy = PlatformPermissionKeys.CommerceManage)]
    public async Task<ActionResult<CommercialSummaryDto>> RegisterOrganization(Guid ownerId, RegisterCommercialContractCommand command, CancellationToken ct) =>
        StatusCode(201, await service.RegisterAsync(Actor, Session, SubscriptionOwnerType.Organization, ownerId, command, ct));
    [HttpPost("users/{ownerId:guid}/commercial-contracts"), Authorize(Policy = PlatformPermissionKeys.CommerceManage)]
    public async Task<ActionResult<CommercialSummaryDto>> RegisterIndividual(Guid ownerId, RegisterCommercialContractCommand command, CancellationToken ct) =>
        StatusCode(201, await service.RegisterAsync(Actor, Session, SubscriptionOwnerType.Individual, ownerId, command, ct));
    [HttpGet("history/{targetId:guid}"), Authorize(Policy = PlatformPermissionKeys.CommerceRead)]
    public async Task<ActionResult<IReadOnlyList<CommercialHistoryDto>>> History(Guid targetId, CancellationToken ct) =>
        Ok(await service.HistoryAsync(Actor, Session, targetId, ct));
}
