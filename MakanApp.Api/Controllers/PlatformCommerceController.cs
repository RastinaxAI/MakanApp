using MakanApp.Api.Authentication;
using MakanApp.Application.Platform;
using MakanApp.Domain.Commerce;
using MakanApp.Domain.Platform;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MakanApp.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/admin/v1/commerce")]
public sealed class PlatformCommerceController(
    IPlatformCommerceOperationsService service) : ControllerBase
{
    [HttpGet("plans")]
    [Authorize(Policy = PlatformPermissionKeys.CommerceRead)]
    public async Task<ActionResult<PlatformPlanPage>> GetPlans(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 30,
        CancellationToken cancellationToken = default) =>
        Ok(await service.GetPlansAsync(new PlatformPlanQuery(search, page, pageSize), cancellationToken));

    [HttpGet("plans/{planId:guid}")]
    [Authorize(Policy = PlatformPermissionKeys.CommerceRead)]
    public async Task<ActionResult<PlatformPlanDetail>> GetPlan(
        Guid planId,
        CancellationToken cancellationToken) =>
        Ok(await service.GetPlanAsync(planId, cancellationToken));

    [HttpPost("plans")]
    [Authorize(Policy = PlatformPermissionKeys.CommerceManage)]
    public async Task<ActionResult<PlatformPlanDetail>> CreatePlan(
        CreatePlatformPlanCommand command,
        CancellationToken cancellationToken)
    {
        var result = await service.CreatePlanAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            command,
            HttpContext.TraceIdentifier,
            cancellationToken);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpPost("plans/{planId:guid}/versions")]
    [Authorize(Policy = PlatformPermissionKeys.CommerceManage)]
    public async Task<ActionResult<PlatformPlanVersionItem>> CreatePlanVersion(
        Guid planId,
        CreatePlatformPlanVersionCommand command,
        CancellationToken cancellationToken)
    {
        var result = await service.CreatePlanVersionAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            planId,
            command,
            HttpContext.TraceIdentifier,
            cancellationToken);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpPost("plan-versions/{planVersionId:guid}/publish")]
    [Authorize(Policy = PlatformPermissionKeys.CommerceManage)]
    public async Task<ActionResult<PlatformPlanVersionItem>> PublishPlanVersion(
        Guid planVersionId,
        PublishPlatformPlanVersionCommand command,
        CancellationToken cancellationToken) =>
        Ok(await service.PublishPlanVersionAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            planVersionId,
            command,
            HttpContext.TraceIdentifier,
            cancellationToken));

    [HttpGet("subscriptions")]
    [Authorize(Policy = PlatformPermissionKeys.CommerceRead)]
    public async Task<ActionResult<PlatformSubscriptionPage>> GetSubscriptions(
        [FromQuery] Guid? organizationId,
        [FromQuery] SubscriptionStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 30,
        CancellationToken cancellationToken = default) =>
        Ok(await service.GetSubscriptionsAsync(
            new PlatformSubscriptionQuery(organizationId, status, page, pageSize),
            cancellationToken));

    [HttpPost("organizations/{organizationId:guid}/subscription-override")]
    [Authorize(Policy = PlatformPermissionKeys.CommerceManage)]
    public async Task<ActionResult<PlatformSubscriptionOverrideResult>> OverrideSubscription(
        Guid organizationId,
        OverridePlatformSubscriptionCommand command,
        CancellationToken cancellationToken) =>
        Ok(await service.OverrideSubscriptionAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            organizationId,
            command,
            HttpContext.TraceIdentifier,
            cancellationToken));
}
