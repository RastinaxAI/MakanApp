using MakanApp.Api.Authentication;
using MakanApp.Application.Commerce;
using MakanApp.Domain.Commerce;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MakanApp.Api.Controllers;

[ApiController, Authorize, Route("api/v1/commerce")]
public sealed class CommercialSubscriptionsController(CommercialSubscriptionService service, IOrderPaymentService payments) : ControllerBase
{
    private Guid Actor => AuthenticatedSession.GetUserId(User);
    private Guid Session => AuthenticatedSession.GetSessionId(User);
    [HttpPost("personal/quote")]
    public async Task<ActionResult<CommercialQuoteDto>> PersonalQuote(CreateOrderCommand command, CancellationToken ct) =>
        Ok(await payments.QuoteAsync(Actor, Session, command, ct, SubscriptionOwnerType.Individual));
    [HttpPost("organization/quote")]
    public async Task<ActionResult<CommercialQuoteDto>> OrganizationQuote(CreateOrderCommand command, CancellationToken ct) =>
        Ok(await payments.QuoteAsync(Actor, Session, command, ct, SubscriptionOwnerType.Organization));
    [HttpGet("catalog")]
    public async Task<ActionResult<IReadOnlyList<CommercialPlanDto>>> Catalog([FromQuery] SubscriptionOwnerType audience, CancellationToken ct) =>
        Ok(await service.CatalogAsync(Actor, Session, audience, ct));
    [HttpGet("personal/subscription")]
    public async Task<ActionResult<CommercialSummaryDto>> Personal(CancellationToken ct) =>
        Ok(await service.SummaryAsync(Actor, Session, SubscriptionOwnerType.Individual, ct));
    [HttpGet("organization/subscription")]
    public async Task<ActionResult<CommercialSummaryDto>> Organization(CancellationToken ct) =>
        Ok(await service.SummaryAsync(Actor, Session, SubscriptionOwnerType.Organization, ct));
    [HttpPost("ai/simple-text/preflight")]
    public async Task<ActionResult<AiQuotaPreflightDto>> Preflight(AiQuotaPreflightCommand command, CancellationToken ct) =>
        Ok(await service.PreflightAsync(Actor, Session, command, ct));
    [HttpPost("personal/orders")]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("payment")]
    public async Task<ActionResult<CreateOrderResult>> CreatePersonalOrder(CreateOrderCommand command, CancellationToken ct)
    {
        var result = await payments.CreateOrderAsync(Actor, Session, command, ct, SubscriptionOwnerType.Individual);
        return StatusCode(result.Replayed ? 200 : 201, result);
    }
    [HttpGet("personal/orders")]
    public async Task<ActionResult<OrderListResult>> Orders([FromQuery] int limit = 20, CancellationToken ct = default) =>
        Ok(await payments.ListOrdersAsync(Actor, Session, limit, ct, SubscriptionOwnerType.Individual));
    [HttpGet("personal/orders/{orderId:guid}")]
    public async Task<ActionResult<OrderDto>> Order(Guid orderId, CancellationToken ct) =>
        Ok(await payments.GetOrderAsync(Actor, Session, orderId, ct, SubscriptionOwnerType.Individual));
    [HttpPost("personal/orders/{orderId:guid}/payments")]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("payment")]
    public async Task<ActionResult<InitiatePaymentResult>> Pay(Guid orderId, InitiatePaymentCommand command, CancellationToken ct) =>
        Ok(await payments.InitiatePaymentAsync(Actor, Session, orderId, command, ct, SubscriptionOwnerType.Individual));
    [HttpPost("personal/subscription/downgrade")]
    public async Task<ActionResult<CommercialSummaryDto>> PersonalDowngrade(ScheduleDowngradeCommand command, CancellationToken ct) =>
        Ok(await service.ScheduleDowngradeAsync(Actor, Session, SubscriptionOwnerType.Individual, command, ct));
    [HttpPost("organization/subscription/downgrade")]
    public async Task<ActionResult<CommercialSummaryDto>> OrganizationDowngrade(ScheduleDowngradeCommand command, CancellationToken ct) =>
        Ok(await service.ScheduleDowngradeAsync(Actor, Session, SubscriptionOwnerType.Organization, command, ct));
}
